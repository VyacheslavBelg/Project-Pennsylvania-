using System.Text;
using System.Text.Json;
using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Max;
using Domovoy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Api.Bot;

/// <summary>
/// Основной сценарий: от описания проблемы до сформированного обращения с идущим сроком.
///
/// Здесь живёт ценность продукта. Зона ответственности и норматив определяются типом
/// проблемы и уточняющим ответом, а не названием управляющей организации, поэтому
/// сценарий работает для любого дома России.
/// </summary>
public sealed class ProblemScenario(
    DomovoyDbContext db,
    ResponsibilityResolver resolver,
    BotScreen screen,
    ILogger<ProblemScenario> logger)
{
    public static class Steps
    {
        public const string ChoosingCategory = "problem:category";
        public const string Clarifying = "problem:clarify";
        public const string DescribingProblem = "problem:describe";
        public const string ConfirmingSubmission = "problem:confirm";
    }

    public static class Callbacks
    {
        public const string Start = "problem:start";
        public const string Category = "problem:cat:";
        public const string Option = "problem:opt:";
        public const string SkipDescription = "problem:skip";
        public const string Submitted = "problem:sent:";
        public const string Answered = "problem:answered:";
        public const string Escalate = "problem:escalate:";
        public const string BackToMenu = "problem:back:menu";
        public const string BackToCategories = "problem:back:cats";
    }

    /// <summary>Категория повторных обращений: у неё есть вход в уже поданные через бота.</summary>
    private const string FollowUpCategory = "followup";

    /// <summary>Состояние сценария между шагами. Лежит в базе, поэтому переживает перезапуск.</summary>
    private sealed record Draft(int CategoryId, int? OptionId, int? RequestId);

    public async Task StartAsync(AppUser user, CancellationToken ct)
    {
        var building = await GetBuildingAsync(user, ct);
        if (building is null)
        {
            await SendAsync(
                "Сначала нужно привязать дом — от него зависят применимые правила.", ct: ct);
            return;
        }

        var categories = await resolver.GetCategoriesAsync(ct);

        var buttons = categories
            .Select(c => new List<object> { MaxButton.Callback(c.Title, $"{Callbacks.Category}{c.Id}") })
            .ToList();

        // Возврат есть на каждом шаге: случайное нажатие не должно загонять в тупик.

        await SetStepAsync(user, Steps.ChoosingCategory, null, ct);
        await SendAsync("Что случилось?", buttons, ct);

        await LogAsync("scenario_started", user, building.Id, null, ct);
    }

    public async Task HandleCategoryAsync(AppUser user, int categoryId, CancellationToken ct)
    {
        var category = await resolver.GetCategoryAsync(categoryId, ct);
        if (category is null)
        {
            await SendAsync("Категория не найдена, начните заново.", ct: ct);
            return;
        }

        await LogAsync("category_selected", user, null, category.Code, ct);

        if (category.ClarifyingOptions.Count == 0)
        {
            await ResolveAndShowAsync(user, category.Id, null, ct);
            return;
        }

        var buttons = category.ClarifyingOptions
            .Select(o => new List<object> { MaxButton.Callback(o.Text, $"{Callbacks.Option}{o.Id}") })
            .ToList();

        // Обращения, поданные через бота, уже лежат в «Моих обращениях» — со сроком
        // и готовой жалобой в инспекцию. Описывать их заново незачем.
        if (category.Code == FollowUpCategory)
        {
            buttons.Insert(0, [MaxButton.Callback("Мои обращения в Домовом", ProfileScenario.Callbacks.MyRequests)]);
        }

        buttons.Add([MaxButton.Callback("‹ К списку проблем", Callbacks.BackToCategories)]);

        await SetStepAsync(user, Steps.Clarifying, new Draft(category.Id, null, null), ct);
        await SendAsync(
            category.ClarifyingQuestion ?? "Уточните ситуацию", buttons, ct);
    }

    public Task HandleOptionAsync(AppUser user, int optionId, CancellationToken ct) =>
        ResolveAndShowAsync(user, null, optionId, ct);

    /// <summary>Выход из сценария: шаг и черновик сбрасываются.</summary>
    public Task CancelAsync(AppUser user, CancellationToken ct) =>
        SetStepAsync(user, null, null, ct);

    private async Task ResolveAndShowAsync(AppUser user, int? categoryId, int? optionId, CancellationToken ct)
    {
        var building = await GetBuildingAsync(user, ct);
        if (building is null) return;

        var option = optionId is { } oid ? await resolver.GetOptionAsync(oid, ct) : null;
        var category = option?.ProblemCategory
            ?? (categoryId is { } cid ? await resolver.GetCategoryAsync(cid, ct) : null);

        if (category is null)
        {
            await SendAsync("Не удалось определить категорию, начните заново.", ct: ct);
            return;
        }

        var resolution = await resolver.ResolveAsync(category, option, building, ct);
        if (resolution is null)
        {
            await SendAsync(
                "Для этой ситуации у меня пока нет правила. Опишите проблему словами — "
                + "передам её как обращение общего порядка.", ct: ct);
            return;
        }

        // Авария: форма здесь неуместна, человеку нужен телефон прямо сейчас.
        if (resolution.IsEmergency)
        {
            await ShowEmergencyAsync(user, resolution, ct);
            return;
        }

        var deadline = resolution.Deadline is { } d
            ? DeadlineCalculator.Compute(DateTimeOffset.UtcNow, d)
            : (DateTimeOffset?)null;

        await LogAsync("resolution_shown", user, building.Id, category.Code, ct);

        var text = ResponsibilityResolver.Describe(resolution, deadline);

        // Зона собственника: обращение в управляющую организацию не нужно, поэтому
        // и черновик не заводим — иначе он навсегда повиснет неотправленным.
        if (resolution.Zone.Code == "resident")
        {
            await SetStepAsync(user, null, null, ct);
            await SendAsync(text,
                [[MaxButton.Callback("Сообщить о другой проблеме", Callbacks.Start)]], ct);
            return;
        }

        // Брошенный черновик остаётся, если пользователь ушёл с экрана описания.
        // Копить их незачем: на список они не попадают, а место занимают.
        var abandoned = await db.Requests
            .Where(r => r.AppUserId == user.Id && r.Status == RequestStatus.Draft)
            .ToListAsync(ct);
        if (abandoned.Count > 0)
        {
            db.Requests.RemoveRange(abandoned);
        }

        var request = new Request
        {
            AppUserId = user.Id,
            BuildingId = building.Id,
            ProblemCategoryId = category.Id,
            ClarifyingOptionId = option?.Id,
            ResponsibilityZoneId = resolution.Zone.Id,
            Subject = option?.Text ?? category.Title,
            Status = RequestStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            DeadlineDescription = resolution.Deadline?.Describe(),
            DeadlineLegalBasis = resolution.Deadline?.LegalBasis
        };

        db.Requests.Add(request);
        await db.SaveChangesAsync(ct);

        await SetStepAsync(user, Steps.DescribingProblem, new Draft(category.Id, option?.Id, request.Id), ct);

        await SendAsync(
            text + "\n\n———\n\nОпишите проблему своими словами — я соберу обращение. "
                 + "Или нажмите «Без описания», и я сформирую его по категории.",
            [
                [MaxButton.Callback("Без описания", Callbacks.SkipDescription)],
                [MaxButton.Callback("‹ К списку проблем", Callbacks.BackToCategories)]
            ], ct);
    }

    private async Task ShowEmergencyAsync(AppUser user, Resolution r, CancellationToken ct)
    {
        var sb = new StringBuilder();
        // Строки не переносятся вручную: на телефоне MAX переносит сам, а ручной
        // перенос посреди фразы превращает текст в лесенку.
        sb.AppendLine("⚠️ Это аварийная ситуация.");
        sb.AppendLine();
        sb.AppendLine("Звоните в аварийно-диспетчерскую службу дома — оператор обязан ответить "
                      + "в течение 5 минут (ПП РФ от 27.03.2018 № 331).");
        sb.AppendLine();

        var phone = r.Building.ManagingOrganization?.EmergencyPhone;
        sb.AppendLine(!string.IsNullOrWhiteSpace(phone)
            ? $"Аварийная служба вашего дома: {phone}"
            : "Телефон аварийной службы указан в квитанции и на доске объявлений в подъезде.");

        sb.AppendLine();
        sb.AppendLine("При угрозе жизни — 112.");
        sb.AppendLine("При запахе газа — 104 или 112: не включайте свет и электроприборы, "
                      + "звоните, выйдя из помещения.");
        sb.AppendLine();
        sb.AppendLine("Запишите номер заявки и время звонка: они понадобятся, если проблему не устранят.");

        await SetStepAsync(user, null, null, ct);
        await LogAsync("emergency_routed", user, r.Building.Id, r.Category.Code, ct);

        await SendAsync(sb.ToString().TrimEnd(),
            [[MaxButton.Callback("Оформить обращение после звонка", Callbacks.Start)]], ct);
    }

    public async Task HandleDescriptionAsync(AppUser user, string? description, CancellationToken ct)
    {
        var draft = ReadDraft(user);
        if (draft?.RequestId is not { } requestId)
        {
            await SendAsync("Сессия потерялась, начните заново.", ct: ct);
            return;
        }

        var request = await db.Requests
            .Include(r => r.Building).ThenInclude(b => b.Address)
            .Include(r => r.Building).ThenInclude(b => b.ManagingOrganization)
            .Include(r => r.ProblemCategory)
            .Include(r => r.ClarifyingOption)
            .Include(r => r.ResponsibilityZone)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request is null)
        {
            await SendAsync("Обращение не найдено, начните заново.", ct: ct);
            return;
        }

        request.Description = description;
        var apartment = await GetApartmentAsync(user, request.BuildingId, ct);
        request.GeneratedText = BuildRequestText(request, user, apartment);
        await db.SaveChangesAsync(ct);

        await SetStepAsync(user, Steps.ConfirmingSubmission, draft with { RequestId = request.Id }, ct);

        await SendAsync(
            "Готовый текст обращения — скопируйте и отправьте в управляющую организацию:\n\n"
            + "———\n" + request.GeneratedText + "\n———\n\n"
            + "Отправить обращение за вас я не могу: канала в системы управляющих организаций "
            + "нет. Когда отправите — нажмите кнопку, и я начну считать срок.",
            [
                [MaxButton.Callback("Я отправил обращение", $"{Callbacks.Submitted}{request.Id}")],
                [MaxButton.Callback("Сообщить о другой проблеме", Callbacks.Start)]
            ], ct);
    }

    public async Task HandleSubmittedAsync(AppUser user, int requestId, CancellationToken ct)
    {
        var request = await db.Requests
            .Include(r => r.ProblemCategory).ThenInclude(c => c.Deadlines)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.AppUserId == user.Id, ct);

        if (request is null)
        {
            await SendAsync("Обращение не найдено.", ct: ct);
            return;
        }

        // Повторное нажатие не должно переподавать обращение: сменился бы номер,
        // а отсчёт срока начался бы заново — то есть нарушение отодвинулось бы.
        if (request.Status != RequestStatus.Draft)
        {
            await SendAsync($"Обращение №{request.Number} уже отправлено, срок считается "
                + $"с {DateText.ShortDate(request.SubmittedAt ?? request.CreatedAt)}.",
                [[MaxButton.Callback("Мои обращения", ProfileScenario.Callbacks.MyRequests)]], ct);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        request.Status = RequestStatus.Submitted;
        request.SubmittedAt = now;
        request.Number = (await db.Requests
            .Where(r => r.AppUserId == user.Id)
            .MaxAsync(r => (int?)r.Number, ct) ?? 0) + 1;

        var deadline = ResponsibilityResolver.PickDeadline(
            request.ProblemCategory.Deadlines, request.ClarifyingOptionId);
        request.DeadlineAt = deadline is not null
            ? DeadlineCalculator.Compute(now, deadline)
            : now.AddDays(30);

        await db.SaveChangesAsync(ct);
        await SetStepAsync(user, null, null, ct);
        await LogAsync("request_submitted", user, request.BuildingId, request.ProblemCategory.Code, ct);

        logger.LogInformation("Обращение {Request} отправлено, срок до {Deadline}", request.Id, request.DeadlineAt);

        await SendAsync(
            $"Обращение №{request.Number} принято к отсчёту.\n\n"
            + $"Ответ должен поступить до {DateText.DateTime(request.DeadlineAt!.Value)}"
            + $" — {request.DeadlineDescription}, основание: {request.DeadlineLegalBasis}.\n\n"
            + "Напомню за день до истечения. Если ответа не будет — соберу пакет для жалобы "
            + "в жилищную инспекцию.",
            [
                [MaxButton.Callback("Мои обращения", ProfileScenario.Callbacks.MyRequests)],
                [MaxButton.Callback("Сообщить о другой проблеме", Callbacks.Start)]
            ], ct);
    }

    public async Task HandleAnsweredAsync(AppUser user, int requestId, CancellationToken ct)
    {
        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == requestId && r.AppUserId == user.Id, ct);
        if (request is null)
        {
            await SendAsync("Обращение не найдено.",
                [[MaxButton.Callback("Мои обращения", ProfileScenario.Callbacks.MyRequests)]], ct);
            return;
        }

        request.Status = RequestStatus.Answered;
        await db.SaveChangesAsync(ct);

        await SendAsync(
            $"Обращение №{request.Number}: отметил, что ответ получен. Если проблему "
            + "не решили по существу — можно подать обращение заново.",
            [
                [MaxButton.Callback("Мои обращения", ProfileScenario.Callbacks.MyRequests)],
                [MaxButton.Callback("Сообщить о другой проблеме", Callbacks.Start)]
            ], ct);
    }

    public async Task HandleEscalateAsync(AppUser user, int requestId, CancellationToken ct)
    {
        var request = await db.Requests
            .Include(r => r.Building).ThenInclude(b => b.Address)
            .Include(r => r.ProblemCategory)
            .Include(r => r.ResponsibilityZone)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.AppUserId == user.Id, ct);

        if (request is null)
        {
            await SendAsync("Обращение не найдено.",
                [[MaxButton.Callback("Мои обращения", ProfileScenario.Callbacks.MyRequests)]], ct);
            return;
        }

        // Повторный показ уже собранной жалобы — не новая эскалация.
        if (request.Status != RequestStatus.Escalated)
        {
            request.Status = RequestStatus.Escalated;
            await db.SaveChangesAsync(ct);
            await LogAsync("escalation_opened", user, request.BuildingId, request.ProblemCategory.Code, ct);
        }

        var apartment = await GetApartmentAsync(user, request.BuildingId, ct);

        await SendAsync(BuildEscalationText(request, user, apartment),
            [
                [MaxButton.Callback("Мои обращения", ProfileScenario.Callbacks.MyRequests)],
                [MaxButton.Callback("Сообщить о другой проблеме", Callbacks.Start)]
            ], ct);
    }

    /// <summary>Текст обращения. Собирается из того, что пользователь уже сообщил.</summary>
    private static string BuildRequestText(Request request, AppUser user, string? apartment)
    {
        var sb = new StringBuilder();
        var address = request.Building.Address.ToString();

        sb.AppendLine(request.ResponsibilityZone?.Addressee ?? "В управляющую организацию");
        if (request.Building.ManagingOrganization is { } org)
        {
            sb.AppendLine(org.Name);
        }
        sb.AppendLine();
        sb.AppendLine($"От: {Who(user)}");
        sb.AppendLine($"Адрес: {address}{ApartmentSuffix(apartment)}");
        if (!string.IsNullOrWhiteSpace(user.ContactInfo))
        {
            sb.AppendLine($"Контакт для ответа: {user.ContactInfo}");
        }
        sb.AppendLine();
        sb.AppendLine("ОБРАЩЕНИЕ");
        sb.AppendLine();
        // Суть — тема, а не категория: «Вода, отопление, электричество» в документе
        // ничего не сообщает, «холодно у меня и у соседей» — сообщает.
        sb.AppendLine($"Сообщаю о проблеме по адресу {address}: "
                      + $"{TextCase.LowerFirst(request.Subject ?? request.ProblemCategory.Title)}.");

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            sb.AppendLine();
            sb.AppendLine(request.Description);
        }

        sb.AppendLine();
        sb.AppendLine("Прошу устранить нарушение и сообщить о принятых мерах.");

        if (request.DeadlineDescription is { Length: > 0 })
        {
            sb.AppendLine($"Нормативный срок ответа — {request.DeadlineDescription} "
                          + $"({request.DeadlineLegalBasis}).");
        }

        sb.AppendLine();
        sb.AppendLine($"Дата: {DateText.DocumentDate(DateTimeOffset.Now)}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Пакет для жалобы в инспекцию. Это тот шаг, на котором житель обычно сдаётся:
    /// нужно собрать даты, нормы и переписку. Здесь всё уже собрано.
    /// </summary>
    private static string BuildEscalationText(Request request, AppUser user, string? apartment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Срок нарушен. Вот готовое обращение в жилищную инспекцию:");
        sb.AppendLine();
        sb.AppendLine("———");
        sb.AppendLine("В Государственную жилищную инспекцию");
        sb.AppendLine($"От: {Who(user)}");
        sb.AppendLine($"Адрес: {request.Building.Address}{ApartmentSuffix(apartment)}");
        sb.AppendLine();
        sb.AppendLine("ЖАЛОБА");
        sb.AppendLine();
        sb.AppendLine($"Обращение по вопросу «{request.Subject ?? request.ProblemCategory.Title}» направлено "
                      + $"{DateText.Date(request.SubmittedAt!.Value)}.");
        sb.AppendLine($"Нормативный срок ответа — {request.DeadlineDescription} "
                      + $"({request.DeadlineLegalBasis}).");
        sb.AppendLine($"Срок истёк {DateText.Date(request.DeadlineAt!.Value)}. Ответ не получен.");
        sb.AppendLine();
        sb.AppendLine("Прошу провести проверку и принять меры реагирования.");
        sb.AppendLine();
        sb.AppendLine($"Дата: {DateText.DocumentDate(DateTimeOffset.Now)}");
        sb.AppendLine("———");
        sb.AppendLine();
        sb.AppendLine("Подать жалобу можно через ГИС ЖКХ (dom.gosuslugi.ru), портал "
                      + "«Госуслуги. Решаем вместе» или сайт инспекции вашего региона.");

        return sb.ToString().TrimEnd();
    }

    /// <summary>ФИО из профиля: имя в MAX для официального документа не годится.</summary>
    private static string Who(AppUser user) =>
        !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.DisplayName ?? "жителя";

    private static string ApartmentSuffix(string? apartment) =>
        string.IsNullOrWhiteSpace(apartment) ? string.Empty : $", кв. {apartment}";

    private Task<string?> GetApartmentAsync(AppUser user, int buildingId, CancellationToken ct) =>
        db.UserBuildingLinks
            .Where(l => l.AppUserId == user.Id && l.BuildingId == buildingId)
            .Select(l => l.Apartment)
            .FirstOrDefaultAsync(ct);

    private Task<Building?> GetBuildingAsync(AppUser user, CancellationToken ct) =>
        db.UserBuildingLinks
            .Where(l => l.AppUserId == user.Id)
            .Include(l => l.Building).ThenInclude(b => b.Address)
            .Include(l => l.Building).ThenInclude(b => b.ManagingOrganization)
            .Select(l => l.Building)
            .FirstOrDefaultAsync(ct);

    private Draft? ReadDraft(AppUser user) =>
        user.DialogState?.PayloadJson is { Length: > 0 } json
            ? JsonSerializer.Deserialize<Draft>(json)
            : null;

    private async Task SetStepAsync(AppUser user, string? step, Draft? draft, CancellationToken ct)
    {
        var state = user.DialogState
            ?? await db.DialogStates.FirstOrDefaultAsync(s => s.AppUserId == user.Id, ct);

        var payload = draft is null ? null : JsonSerializer.Serialize(draft);

        if (state is null)
        {
            state = new DialogState
            {
                AppUserId = user.Id,
                Step = step ?? "idle",
                PayloadJson = payload,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.DialogStates.Add(state);
        }
        else
        {
            state.Step = step ?? "idle";
            state.PayloadJson = payload;
            state.UpdatedAt = DateTimeOffset.UtcNow;
        }

        user.DialogState = state;
        await db.SaveChangesAsync(ct);
    }

    private async Task LogAsync(string name, AppUser user, int? buildingId, string? categoryCode,
        CancellationToken ct)
    {
        db.TelemetryEvents.Add(new TelemetryEvent
        {
            Name = name,
            MaxUserId = user.MaxUserId,
            BuildingId = buildingId,
            CategoryCode = categoryCode,
            OccurredAt = DateTimeOffset.UtcNow
        });

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
