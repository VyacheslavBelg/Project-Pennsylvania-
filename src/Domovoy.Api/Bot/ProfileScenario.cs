using System.Text;
using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Max;
using Domovoy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Api.Bot;

/// <summary>
/// Профиль жителя и список его обращений.
///
/// ФИО и номер квартиры здесь не формальность: порядок рассмотрения обращений граждан
/// требует указывать фамилию, имя, отчество и адрес для ответа. Без них обращение
/// можно оставить без рассмотрения, то есть сформированный нами текст был бы
/// юридически бесполезен.
/// </summary>
public sealed class ProfileScenario(
    DomovoyDbContext db,
    BotScreen screen,
    WebForms forms)
{
    /// <summary>Экран мини-приложения с полями ФИО и квартиры.</summary>
    private const string ProfileForm = "profile";

    /// <summary>Экран мини-приложения с карточками обращений и живым отсчётом срока.</summary>
    private const string RequestsScreen = "requests";

    public static class Steps
    {
        public const string AwaitingFullName = "profile:name";
        public const string AwaitingApartment = "profile:apartment";
    }

    public static class Callbacks
    {
        public const string Show = "profile:show";
        public const string EditName = "profile:name";
        public const string EditApartment = "profile:apartment";
        public const string MyRequests = "profile:requests";
        public const string Card = "profile:card:";
        public const string Skip = "profile:skip";
    }

    /// <param name="notice">Подтверждение предыдущего действия — строкой над профилем,
    /// а не отдельным сообщением, которое тут же сменилось бы экраном.</param>
    public async Task ShowAsync(AppUser user, CancellationToken ct, string? notice = null)
    {
        var link = await db.UserBuildingLinks
            .Include(l => l.Building).ThenInclude(b => b.Address)
            .FirstOrDefaultAsync(l => l.AppUserId == user.Id, ct);

        var sb = new StringBuilder();
        if (notice is { Length: > 0 })
        {
            sb.AppendLine($"✅ {notice}");
            sb.AppendLine();
        }
        sb.AppendLine("Ваши данные для обращений");
        sb.AppendLine();
        sb.AppendLine($"ФИО: {user.FullName ?? "не указано"}");
        sb.AppendLine($"Дом: {link?.Building.Address.ToFullString() ?? "не привязан"}");
        sb.AppendLine($"Квартира: {link?.Apartment ?? "не указана"}");

        if (user.FullName is null or { Length: 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Без ФИО и адреса обращение могут оставить без рассмотрения: "
                          + "этого требует порядок работы с обращениями граждан.");
        }

        var buttons = new List<List<object>>();

        // Форма заполняет оба поля разом и не оставляет следов в переписке.
        if (forms.Enabled)
        {
            buttons.Add([forms.Button("✍️ Заполнить в форме", ProfileForm)]);
        }
        else
        {
            buttons.Add([MaxButton.Callback("Указать ФИО", Callbacks.EditName)]);
            buttons.Add([MaxButton.Callback("Указать квартиру", Callbacks.EditApartment)]);
        }

        buttons.Add([MaxButton.Callback("Мои обращения", Callbacks.MyRequests)]);

        await SendAsync(sb.ToString().TrimEnd(), buttons, ct);
    }

    public async Task AskFullNameAsync(AppUser user, CancellationToken ct)
    {
        await SetStepAsync(user, Steps.AwaitingFullName, ct);
        await SendAsync(
            "Напишите фамилию, имя и отчество — они попадут в текст обращения.\n\n"
            + "Например: Иванов Иван Иванович",
            [[MaxButton.Callback("Пропустить", Callbacks.Skip)]], ct);
    }

    public async Task AskApartmentAsync(AppUser user, CancellationToken ct)
    {
        await SetStepAsync(user, Steps.AwaitingApartment, ct);
        await SendAsync(
            "Напишите номер квартиры — он нужен как адрес для ответа.",
            [[MaxButton.Callback("Пропустить", Callbacks.Skip)]], ct);
    }

    public async Task SaveFullNameAsync(AppUser user, string value, CancellationToken ct)
    {
        user.FullName = value.Trim();
        await SetStepAsync(user, null, ct);
        await db.SaveChangesAsync(ct);

        await ShowAsync(user, ct, $"Записал: {user.FullName}");
    }

    public async Task SaveApartmentAsync(AppUser user, string value, CancellationToken ct)
    {
        var link = await db.UserBuildingLinks.FirstOrDefaultAsync(l => l.AppUserId == user.Id, ct);

        if (link is null)
        {
            await SendAsync("Сначала привяжите дом.", ct: ct);
            return;
        }

        link.Apartment = value.Trim();
        await SetStepAsync(user, null, ct);
        await db.SaveChangesAsync(ct);

        await ShowAsync(user, ct, $"Записал квартиру: {link.Apartment}");
    }

    /// <summary>
    /// Сохранение обоих полей разом — из формы мини-приложения, где они на одном экране.
    /// Пустое значение означает «не меняем»: форма шлёт только заполненные поля.
    /// </summary>
    public async Task SaveProfileAsync(AppUser user, string? fullName, string? apartment, CancellationToken ct)
    {
        var saved = new List<string>();

        if (fullName is { Length: > 0 })
        {
            user.FullName = fullName.Trim();
            saved.Add("ФИО");
        }

        if (apartment is { Length: > 0 })
        {
            var link = await db.UserBuildingLinks.FirstOrDefaultAsync(l => l.AppUserId == user.Id, ct);
            if (link is null)
            {
                await SendAsync("Сначала привяжите дом.", ct: ct);
                return;
            }

            link.Apartment = apartment.Trim();
            saved.Add("квартиру");
        }

        await SetStepAsync(user, null, ct);
        await db.SaveChangesAsync(ct);

        await ShowAsync(user, ct, saved.Count > 0 ? $"Записал {string.Join(" и ", saved)}" : null);
    }

    public async Task SkipAsync(AppUser user, CancellationToken ct)
    {
        await SetStepAsync(user, null, ct);
        await ShowAsync(user, ct);
    }

    /// <summary>
    /// Список обращений.
    ///
    /// Группировка по дому здесь не украшение: привязку можно сменить, и тогда в списке
    /// оказываются обращения по разным адресам. Без заголовка непонятно, какое к какому.
    /// </summary>
    public async Task ShowRequestsAsync(AppUser user, CancellationToken ct)
    {
        var requests = await db.Requests
            .Include(r => r.ProblemCategory)
            .Include(r => r.ResponsibilityZone)
            .Include(r => r.Building).ThenInclude(b => b.Address)
            .Where(r => r.AppUserId == user.Id && r.Status != RequestStatus.Draft)
            .OrderByDescending(r => r.SubmittedAt ?? r.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (requests.Count == 0)
        {
            await SendAsync(
                "Отправленных обращений пока нет.",
                [[MaxButton.Callback("Сообщить о проблеме", ProblemScenario.Callbacks.Start)]], ct);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var sb = new StringBuilder();
        sb.Append($"Мои обращения: {requests.Count}");

        var buttons = new List<List<object>>();

        foreach (var atAddress in requests.GroupBy(r => r.Building.Address.ToFullString()))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"🏠 {atAddress.Key}");

            foreach (var r in atAddress)
            {
                sb.AppendLine();
                sb.AppendLine($"№{r.Number} · {r.Subject ?? r.ProblemCategory.Title}");
                sb.AppendLine($"Отвечает: {Lower(r.ResponsibilityZone?.Title) ?? "не определено"}"
                              + $", подано {DateText.ShortDate(r.SubmittedAt ?? r.CreatedAt)}");
                sb.AppendLine(DescribeState(r, now));

                // Действия живут в карточке: иначе при нескольких обращениях
                // под списком вырастает частокол кнопок.
                buttons.Add([MaxButton.Callback(
                    Label($"№{r.Number} · {r.Subject ?? r.ProblemCategory.Title}"), $"{Callbacks.Card}{r.Id}")]);
            }
        }

        // В чате остаток срока устаревает сразу после отправки сообщения,
        // в приложении он пересчитывается на глазах.
        if (forms.Enabled)
        {
            buttons.Add([forms.Button("⏱ Сроки в приложении", RequestsScreen)]);
        }

        buttons.Add([MaxButton.Callback("Сообщить о проблеме", ProblemScenario.Callbacks.Start)]);

        await SendAsync(sb.ToString().TrimEnd(), buttons, ct);
    }

    /// <summary>
    /// Карточка обращения.
    ///
    /// Нужна не ради полноты: действия «отметить ответ» и «собрать жалобу» раньше жили
    /// только в том сообщении, которым бот отвечал в момент события. Стоило пролистать
    /// чат дальше — и отметить ответ было уже нечем. Здесь же снова доступен текст
    /// обращения: без него человек, закрывший чат до того, как скопировал, терял его
    /// насовсем.
    /// </summary>
    public async Task ShowRequestCardAsync(AppUser user, int requestId, CancellationToken ct)
    {
        var r = await db.Requests
            .Include(x => x.ProblemCategory)
            .Include(x => x.ResponsibilityZone)
            .Include(x => x.Building).ThenInclude(b => b.Address)
            .FirstOrDefaultAsync(x => x.Id == requestId && x.AppUserId == user.Id, ct);

        if (r is null)
        {
            await SendAsync("Обращение не найдено.",
                [[MaxButton.Callback("Мои обращения", Callbacks.MyRequests)]], ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Обращение №{r.Number}");
        sb.AppendLine($"🏠 {r.Building.Address.ToFullString()}");
        sb.AppendLine();
        sb.AppendLine(r.Subject ?? r.ProblemCategory.Title);
        sb.AppendLine($"Отвечает: {Lower(r.ResponsibilityZone?.Title) ?? "не определено"}");
        sb.AppendLine($"Подано {DateText.ShortDate(r.SubmittedAt ?? r.CreatedAt)}");
        sb.AppendLine(DescribeState(r, DateTimeOffset.UtcNow));

        if (r.DeadlineDescription is { Length: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"Норматив: {r.DeadlineDescription}");
            sb.AppendLine($"Основание: {r.DeadlineLegalBasis}");
        }

        if (r.GeneratedText is { Length: > 0 } text)
        {
            sb.AppendLine();
            sb.AppendLine("Текст обращения:");
            sb.AppendLine("———");
            sb.AppendLine(text);
            sb.AppendLine("———");
        }

        var buttons = new List<List<object>>();

        if (r.Status is RequestStatus.Submitted or RequestStatus.Breached)
        {
            buttons.Add([MaxButton.Callback("Мне уже ответили",
                $"{ProblemScenario.Callbacks.Answered}{r.Id}")]);
        }

        // Текст жалобы не хранится, а экран один: вернуться к нему можно только отсюда,
        // поэтому кнопка остаётся и после того, как жалоба уже собрана.
        if (r.Status is RequestStatus.Breached or RequestStatus.Escalated)
        {
            buttons.Add([MaxButton.Callback(
                r.Status == RequestStatus.Breached ? "Жалоба в инспекцию" : "Текст жалобы в инспекцию",
                $"{ProblemScenario.Callbacks.Escalate}{r.Id}")]);
        }

        buttons.Add([MaxButton.Callback("Удалить обращение",
            $"{ProblemScenario.Callbacks.Delete}{r.Id}")]);
        buttons.Add([MaxButton.Callback("‹ К списку обращений", Callbacks.MyRequests)]);

        await SendAsync(sb.ToString().TrimEnd(), buttons, ct);
    }

    /// <summary>Подпись кнопки: MAX обрезает длинные, обрезаем сами и осмысленно.</summary>
    private static string Label(string text) =>
        text.Length <= BotUi.MaxButtonLabel ? text : text[..(BotUi.MaxButtonLabel - 1)] + "…";

    /// <summary>
    /// Состояние обращения — ради него список и открывают, поэтому отдельной строкой
    /// и со знаком: «просрочено» должно быть видно, не вчитываясь.
    /// </summary>
    private static string DescribeState(Request r, DateTimeOffset now) => r.Status switch
    {
        RequestStatus.Submitted when r.DeadlineAt is { } d =>
            $"⏳ Ответ до {DateText.ShortDate(d)} — {DeadlineCalculator.DescribeRemaining(d, now)}",
        RequestStatus.Submitted => "⏳ Ждём ответа",
        RequestStatus.Answered => "✅ Ответ получен",
        RequestStatus.Breached when r.DeadlineAt is { } d =>
            $"❗ Срок истёк {DateText.ShortDate(d)}, ответа нет",
        RequestStatus.Breached => "❗ Срок нарушен, можно жаловаться в инспекцию",
        RequestStatus.Escalated => "📨 Жалоба в инспекцию подготовлена",
        RequestStatus.Closed => "Закрыто",
        _ => "Черновик"
    };

    private static string? Lower(string? title) => title is null ? null : TextCase.LowerFirst(title);

    private async Task SetStepAsync(AppUser user, string? step, CancellationToken ct)
    {
        var state = user.DialogState
            ?? await db.DialogStates.FirstOrDefaultAsync(s => s.AppUserId == user.Id, ct);

        if (state is null)
        {
            state = new DialogState
            {
                AppUserId = user.Id,
                Step = step ?? "idle",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.DialogStates.Add(state);
        }
        else
        {
            state.Step = step ?? "idle";
            state.PayloadJson = null;
            state.UpdatedAt = DateTimeOffset.UtcNow;
        }

        user.DialogState = state;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Показ экрана с автоматическим возвратом в меню. Через него идут все экраны сценария,
    /// поэтому забыть про кнопку возврата нельзя.
    /// </summary>
    private Task SendAsync(string text,
        List<List<object>>? buttons = null, CancellationToken ct = default) =>
        screen.ShowAsync(text, BotUi.WithMenu(buttons), ct);
}
