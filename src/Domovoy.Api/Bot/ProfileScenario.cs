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
    IMaxBotClient max)
{
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
        public const string Skip = "profile:skip";
    }

    public async Task ShowAsync(AppUser user, CancellationToken ct)
    {
        var link = await db.UserBuildingLinks
            .Include(l => l.Building).ThenInclude(b => b.Address)
            .FirstOrDefaultAsync(l => l.AppUserId == user.Id, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Ваши данные для обращений");
        sb.AppendLine();
        sb.AppendLine($"ФИО: {user.FullName ?? "не указано"}");
        sb.AppendLine($"Дом: {link?.Building.Address.ToString() ?? "не привязан"}");
        sb.AppendLine($"Квартира: {link?.Apartment ?? "не указана"}");

        if (user.FullName is null or { Length: 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Без ФИО и адреса обращение могут оставить без рассмотрения:");
            sb.AppendLine("этого требует порядок работы с обращениями граждан.");
        }

        await SendAsync(user.MaxChatId, sb.ToString().TrimEnd(),
        [
            [MaxButton.Callback("Указать ФИО", Callbacks.EditName)],
            [MaxButton.Callback("Указать квартиру", Callbacks.EditApartment)],
            [MaxButton.Callback("Мои обращения", Callbacks.MyRequests)]
        ], ct);
    }

    public async Task AskFullNameAsync(AppUser user, CancellationToken ct)
    {
        await SetStepAsync(user, Steps.AwaitingFullName, ct);
        await SendAsync(user.MaxChatId,
            "Напишите фамилию, имя и отчество — они попадут в текст обращения.\n\n"
            + "Например: Иванов Иван Иванович",
            [[MaxButton.Callback("Пропустить", Callbacks.Skip)]], ct);
    }

    public async Task AskApartmentAsync(AppUser user, CancellationToken ct)
    {
        await SetStepAsync(user, Steps.AwaitingApartment, ct);
        await SendAsync(user.MaxChatId,
            "Напишите номер квартиры — он нужен как адрес для ответа.",
            [[MaxButton.Callback("Пропустить", Callbacks.Skip)]], ct);
    }

    public async Task SaveFullNameAsync(AppUser user, string value, CancellationToken ct)
    {
        user.FullName = value.Trim();
        await SetStepAsync(user, null, ct);
        await db.SaveChangesAsync(ct);

        await SendAsync(user.MaxChatId, $"Записал: {user.FullName}", ct: ct);
        await ShowAsync(user, ct);
    }

    public async Task SaveApartmentAsync(AppUser user, string value, CancellationToken ct)
    {
        var link = await db.UserBuildingLinks.FirstOrDefaultAsync(l => l.AppUserId == user.Id, ct);

        if (link is null)
        {
            await SendAsync(user.MaxChatId, "Сначала привяжите дом.", ct: ct);
            return;
        }

        link.Apartment = value.Trim();
        await SetStepAsync(user, null, ct);
        await db.SaveChangesAsync(ct);

        await SendAsync(user.MaxChatId, $"Записал квартиру: {link.Apartment}", ct: ct);
        await ShowAsync(user, ct);
    }

    public async Task SkipAsync(AppUser user, CancellationToken ct)
    {
        await SetStepAsync(user, null, ct);
        await ShowAsync(user, ct);
    }

    /// <summary>Список обращений: раньше пользователь не мог увидеть, что уже отправил.</summary>
    public async Task ShowRequestsAsync(AppUser user, CancellationToken ct)
    {
        var requests = await db.Requests
            .Include(r => r.ProblemCategory)
            .Include(r => r.ResponsibilityZone)
            .Where(r => r.AppUserId == user.Id && r.Status != RequestStatus.Draft)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (requests.Count == 0)
        {
            await SendAsync(user.MaxChatId,
                "Отправленных обращений пока нет.",
                [[MaxButton.Callback("Сообщить о проблеме", ProblemScenario.Callbacks.Start)]], ct);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var sb = new StringBuilder();
        sb.AppendLine($"Ваши обращения: {requests.Count}");

        var buttons = new List<List<object>>();

        foreach (var r in requests)
        {
            sb.AppendLine();
            sb.AppendLine($"№{r.Id} · {r.ProblemCategory.Title}");
            sb.AppendLine($"Отвечает: {r.ResponsibilityZone?.Title ?? "не определено"}");
            sb.AppendLine($"Подано: {DateText.Date(r.SubmittedAt ?? r.CreatedAt)}");
            sb.AppendLine($"Статус: {DescribeStatus(r, now)}");

            // Кнопка эскалации только там, где срок действительно нарушен.
            if (r.Status == RequestStatus.Breached)
            {
                buttons.Add([MaxButton.Callback($"Жалоба по №{r.Id}",
                    $"{ProblemScenario.Callbacks.Escalate}{r.Id}")]);
            }
        }

        buttons.Add([MaxButton.Callback("Сообщить о проблеме", ProblemScenario.Callbacks.Start)]);

        await SendAsync(user.MaxChatId, sb.ToString().TrimEnd(), buttons, ct);
    }

    private static string DescribeStatus(Request r, DateTimeOffset now) => r.Status switch
    {
        RequestStatus.Submitted when r.DeadlineAt is { } d =>
            $"ждём ответа, {DeadlineCalculator.DescribeRemaining(d, now)}",
        RequestStatus.Submitted => "ждём ответа",
        RequestStatus.Answered => "ответ получен",
        RequestStatus.Breached => "срок нарушен, можно жаловаться в инспекцию",
        RequestStatus.Escalated => "жалоба в инспекцию подготовлена",
        RequestStatus.Closed => "закрыто",
        _ => "черновик"
    };

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
    /// Отправка с автоматическим возвратом в меню. Через неё идут все экраны сценария,
    /// поэтому забыть про кнопку возврата нельзя.
    /// </summary>
    private Task SendAsync(long chatId, string text,
        List<List<object>>? buttons = null, CancellationToken ct = default) =>
        max.SendMessageAsync(chatId, text, BotUi.WithMenu(buttons), ct);
}
