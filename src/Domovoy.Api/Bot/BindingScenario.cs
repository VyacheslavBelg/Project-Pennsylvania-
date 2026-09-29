using System.Text;
using System.Text.Json;
using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Max;
using Domovoy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Domovoy.Api.Bot;

/// <summary>
/// Сценарий привязки пользователя к дому — задача 2.5.
///
/// Классификатор проблемы, подача обращения и отсчёт норматива появятся в Фазе 3.
/// Здесь закладывается механика: шаги хранятся в базе, поэтому перезапуск процесса
/// не теряет контекст пользователя.
/// </summary>
public sealed class BindingScenario(
    DomovoyDbContext db,
    AddressLookupService lookup,
    BotScreen screen,
    IOptions<MaxBotOptions> options,
    ILogger<BindingScenario> logger)
{
    public static class Steps
    {
        public const string Idle = "idle";
        public const string AwaitingAddress = "awaiting_address";
    }

    private static class Callbacks
    {
        public const string BindStart = "bind:start";
        public const string BindPick = "bind:pick:";
        public const string BindSuggested = "bind:suggested:";
        public const string BindReset = "bind:reset";
        public const string ShowHouse = "bind:house";
    }

    private readonly MaxBotOptions _options = options.Value;

    /// <summary>Параметр запуска мини-приложения, открывающий форму выбора дома.</summary>
    public const string BindFormStartParam = "bind";

    public async Task HandleAsync(MaxUpdate update, CancellationToken ct)
    {
        switch (update.UpdateType)
        {
            case MaxUpdate.BotStarted:
                if (update.ChatId is { } startedChat)
                {
                    var started = await EnsureUserAsync(update.User?.UserId ?? 0, startedChat, update.User?.Name, ct);
                    await ShowEntryPointAsync(started, ct);
                }
                break;

            case MaxUpdate.MessageCreated:
                await HandleMessageAsync(update, ct);
                break;

            case MaxUpdate.MessageCallback:
                await HandleCallbackAsync(update, ct);
                break;
        }
    }

    private async Task HandleMessageAsync(MaxUpdate update, CancellationToken ct)
    {
        var chatId = update.Message?.Recipient?.ChatId;
        var sender = update.Message?.Sender;
        if (chatId is null || sender is null)
        {
            return;
        }

        var user = await EnsureUserAsync(sender.UserId, chatId.Value, sender.Name, ct);
        var text = update.Message?.Body?.Text?.Trim() ?? string.Empty;

        if (text.StartsWith('/'))
        {
            await SetStepAsync(user, Steps.Idle, ct);
            await ShowEntryPointAsync(user, ct);
            return;
        }

        var step = user.DialogState?.Step ?? Steps.Idle;

        if (step == Steps.AwaitingAddress)
        {
            await SearchAndOfferAsync(user, text, ct);
            return;
        }

        // Произвольный текст не должен выглядеть как команда: повторять всю карточку дома
        // на каждое «привет» сбивает с толку. Короткая подсказка с действиями понятнее.
        await ShowHintAsync(user, ct);
    }

    private async Task ShowHintAsync(AppUser user, CancellationToken ct)
    {
        var hasBuilding = await db.UserBuildingLinks.AnyAsync(l => l.AppUserId == user.Id, ct);

        if (!hasBuilding)
        {
            await SendAsync(
                "Я понимаю команды и кнопки. Начнём с дома — он определяет применимые правила.",
                [[MaxButton.Callback("Привязать дом", Callbacks.BindStart)]], ct);
            return;
        }

        await SendAsync(
            "Свободный текст я пока не разбираю. Выберите действие или отправьте /start.",
            [
                [MaxButton.Callback("Сообщить о проблеме", ProblemScenario.Callbacks.Start)],
                [MaxButton.Callback("Мой дом", Callbacks.ShowHouse)]
            ], ct);
    }

    private async Task HandleCallbackAsync(MaxUpdate update, CancellationToken ct)
    {
        var callback = update.Callback;
        var payload = callback?.Payload;
        if (callback?.CallbackId is null || payload is null || callback.User is null)
        {
            return;
        }

        var chatId = update.Message?.Recipient?.ChatId ?? update.ChatId;
        if (chatId is null)
        {
            return;
        }

        var user = await EnsureUserAsync(callback.User.UserId, chatId.Value, callback.User.Name, ct);

        if (payload == Callbacks.ShowHouse)
        {
            await ShowEntryPointAsync(user, ct);
            return;
        }

        if (payload == Callbacks.BindStart || payload == Callbacks.BindReset)
        {
            // Форма — основной путь: сообщения пользователя бот удалить не может,
            // а ввод в форме в переписке не остаётся. Текст — запасной путь, шаг
            // ожидания адреса открыт в обоих случаях.
            await SetStepAsync(user, Steps.AwaitingAddress, ct);

            if (_options.HasMiniApp && _options.BindForm)
            {
                await SendAsync(
                    "Выберите дом в форме — так в переписке не останется лишних сообщений.\n\n"
                    + "Или напишите адрес сюда — например, «Баумана 15».",
                    [[MaxButton.Link("🏠 Выбрать дом", _options.MiniAppLink(BindFormStartParam))]], ct);
                return;
            }

            await SendAsync(
                "Введите адрес дома — например, «Баумана 15» или «Ямашева 54».", ct: ct);
            return;
        }

        if (payload.StartsWith(Callbacks.BindPick, StringComparison.Ordinal)
            && int.TryParse(payload[Callbacks.BindPick.Length..], out var buildingId))
        {
            await LinkBuildingAsync(user, buildingId, ct);
            return;
        }

        if (payload.StartsWith(Callbacks.BindSuggested, StringComparison.Ordinal)
            && int.TryParse(payload[Callbacks.BindSuggested.Length..], out var suggestedIndex))
        {
            await BindSuggestedAsync(user, suggestedIndex, ct);
        }
    }

    public async Task ShowEntryPointAsync(AppUser user, CancellationToken ct)
    {
        var link = await db.UserBuildingLinks
            .Include(l => l.Building).ThenInclude(b => b.Address)
            .Include(l => l.Building).ThenInclude(b => b.ManagingOrganization)
            .FirstOrDefaultAsync(l => l.AppUserId == user.Id, ct);

        if (link is null)
        {
            // Тоже главное меню, только для непривязанного пользователя: возврат на себя не нужен.
            await screen.ShowAsync(
                "Домовой помогает разобраться, кто отвечает за проблему в доме и в какой срок обязан отреагировать.\n\n"
                + "Начнём с дома — он определяет управляющую организацию и применимые правила.",
                [[MaxButton.Callback("Привязать дом", Callbacks.BindStart)]], ct);
            return;
        }

        // Это и есть главное меню — кнопка возврата на саму себя здесь не нужна.
        await screen.ShowAsync(DescribeBuilding(link.Building), BuildingButtons(link.Building), ct);
    }

    private List<List<object>> BuildingButtons(Building building)
    {
        // Главное действие — первым: ради него продукт и существует.
        List<List<object>> buttons =
        [
            [MaxButton.Callback("Сообщить о проблеме", ProblemScenario.Callbacks.Start)]
        ];

        if (_options.HasMiniApp)
        {
            buttons.Add([MaxButton.Link("Открыть карточку дома", _options.MiniAppLink())]);
        }

        buttons.Add([MaxButton.Callback("Мои обращения", ProfileScenario.Callbacks.MyRequests)]);
        buttons.Add([MaxButton.Callback("Мои данные", ProfileScenario.Callbacks.Show)]);
        buttons.Add([MaxButton.Callback("Выбрать другой дом", Callbacks.BindReset)]);
        return buttons;
    }

    private async Task SearchAndOfferAsync(AppUser user, string query, CancellationToken ct)
    {
        var found = await lookup.FindAsync(query, ct);

        if (found.Count == 0)
        {
            await SendAsync(
                "Такой адрес не найден. Попробуйте указать улицу и номер дома — например, «Баумана 15».",
                ct: ct);
            return;
        }

        // Варианты из адресного реестра ещё не сохранены, поэтому держим их в состоянии
        // диалога, а в кнопку кладём только позицию в списке.
        var suggested = found.Where(c => !c.KnownHouse).Select(c => c.Suggested!).ToList();
        if (suggested.Count > 0)
        {
            await SetStepAsync(user, Steps.AwaitingAddress, ct, JsonSerializer.Serialize(suggested));
        }

        // Кнопка ведёт к варианту по его месту в исходной выдаче: в этом порядке
        // подсказки реестра сохранены в состоянии диалога.
        var payloads = new List<string>(found.Count);
        var suggestedIndex = 0;
        foreach (var candidate in found)
        {
            payloads.Add(candidate.KnownHouse
                ? $"{Callbacks.BindPick}{candidate.BuildingId}"
                : $"{Callbacks.BindSuggested}{suggestedIndex++}");
        }

        var lines = new List<string>();
        var buttons = new List<List<object>>();

        if (found.Count == 1)
        {
            // Нумеровать единственный вариант незачем.
            lines.Add("Нашёлся один дом:");
            lines.Add("");
            lines.Add($"🏠 {found[0].Display}");

            buttons.Add([MaxButton.Callback("Да, это мой дом", payloads[0])]);
        }
        else
        {
            var groups = AddressGrouping.Group(found);

            lines.Add(groups.Count > 1
                ? $"Нашлось домов: {found.Count} в {groups.Count} населённых пунктах"
                : $"Нашлось домов: {found.Count}");

            // Номера сквозные по показанному списку; order помнит, какой вариант
            // стоит за каждым номером.
            var order = new List<int>(found.Count);

            foreach (var group in groups)
            {
                lines.Add("");
                if (group.Header.Length > 0)
                {
                    lines.Add($"🏠 {group.Header}");
                }

                foreach (var item in group.Items)
                {
                    order.Add(item.Index);
                    lines.Add($"{order.Count} — {item.Label}");
                }
            }

            lines.Add("");
            lines.Add("Нажмите номер своего дома.");

            // Больше реестр за один запрос не отдаёт: вариантов может быть больше,
            // и нужного среди показанных может не оказаться. Шаг ввода адреса открыт.
            if (found.Count >= AddressLookupService.MaxResults)
            {
                lines.Add("");
                lines.Add($"Показаны {found.Count} самых подходящих — больше адресный реестр "
                    + "за один запрос не отдаёт. Если вашего дома нет, напишите адрес точнее: "
                    + "с городом, корпусом или строением.");
            }

            // Подпись кнопки — только номер: адрес в неё не помещается, а обрезанный
            // выглядит одинаково у соседних корпусов. Номера идут в ряд.
            const int perRow = 5;

            for (var i = 0; i < order.Count; i += perRow)
            {
                var row = new List<object>();

                for (var j = i; j < Math.Min(i + perRow, order.Count); j++)
                {
                    row.Add(MaxButton.Callback($"{j + 1}", payloads[order[j]]));
                }

                buttons.Add(row);
            }
        }

        if (found.All(c => !c.KnownHouse))
        {
            lines.Add("");
            lines.Add("Адрес распознан по государственному адресному реестру. "
                + "Сведений об управляющей организации у нас пока нет.");
        }

        await SendAsync(string.Join("\n", lines), buttons, ct);
    }

    private async Task BindSuggestedAsync(AppUser user, int index, CancellationToken ct)
    {
        var state = user.DialogState
            ?? await db.DialogStates.FirstOrDefaultAsync(s => s.AppUserId == user.Id, ct);

        var stored = state?.PayloadJson is { Length: > 0 } json
            ? JsonSerializer.Deserialize<List<SuggestedAddress>>(json)
            : null;

        if (stored is null || index < 0 || index >= stored.Count)
        {
            await SetStepAsync(user, Steps.AwaitingAddress, ct);
            await SendAsync(
                "Не удалось восстановить выбранный адрес. Введите его ещё раз.", ct: ct);
            return;
        }

        var building = await lookup.EnsureBuildingAsync(stored[index], ct);
        await LinkBuildingAsync(user, building.Id, ct);
    }

    /// <summary>
    /// Привязывает пользователя к дому и показывает карточку дома. Вызывается и из чата,
    /// и из формы мини-приложения: во втором случае карточка сменяет в чате экран ввода адреса.
    ///
    /// Имя не BindAsync намеренно: публичный метод с таким именем минимальные API ASP.NET
    /// принимают за собственный способ привязки параметра и падают на первом же запросе.
    /// </summary>
    public async Task<Building?> LinkBuildingAsync(AppUser user, int buildingId, CancellationToken ct)
    {
        var building = await db.Buildings
            .Include(b => b.Address)
            .Include(b => b.ManagingOrganization)
            .FirstOrDefaultAsync(b => b.Id == buildingId, ct);

        if (building is null)
        {
            await SendAsync("Этот дом больше недоступен, попробуйте ещё раз.", ct: ct);
            return null;
        }

        var existing = await db.UserBuildingLinks.FirstOrDefaultAsync(l => l.AppUserId == user.Id, ct);
        if (existing is not null)
        {
            db.UserBuildingLinks.Remove(existing);
        }

        db.UserBuildingLinks.Add(new UserBuildingLink
        {
            AppUserId = user.Id,
            BuildingId = building.Id,
            Role = ResidentRole.Resident,
            LinkedAt = DateTimeOffset.UtcNow,
            // Принадлежность к дому в MVP не проверяется — заявленное упрощение.
            IsVerified = false
        });

        db.TelemetryEvents.Add(new TelemetryEvent
        {
            Name = "building_linked",
            MaxUserId = user.MaxUserId,
            BuildingId = building.Id,
            OccurredAt = DateTimeOffset.UtcNow
        });

        await SetStepAsync(user, Steps.Idle, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Пользователь {User} привязан к дому {Building}", user.MaxUserId, building.Id);

        await SendAsync(
            "Дом привязан.\n\n" + DescribeBuilding(building), BuildingButtons(building), ct);

        return building;
    }

    private static string DescribeBuilding(Building b)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🏠 {b.Address.ToFullString()}");

        if (b.ManagingOrganization is { } org)
        {
            sb.AppendLine($"Управление: {org.Name}");
            if (!string.IsNullOrWhiteSpace(org.Phone)) sb.AppendLine($"Телефон: {org.Phone}");
            if (!string.IsNullOrWhiteSpace(org.EmergencyPhone)) sb.AppendLine($"Аварийная служба: {org.EmergencyPhone}");

            var kind = b.ManagementKind switch
            {
                ManagementKind.ManagementCompany => "управляющая организация",
                ManagementKind.Hoa => "ТСЖ",
                ManagementKind.Direct => "непосредственное управление",
                _ => "не указан"
            };
            sb.AppendLine($"Способ управления: {kind}");
        }
        else
        {
            // Отсутствие контактов организации — не отказ: зона ответственности и норматив
            // определяются типом проблемы и способом управления, а эти правила федеральные.
            // Название и телефоны — региональные данные, то есть переменная часть.
            // Строки не переносятся вручную: MAX переносит сам, а ручной перенос посреди
            // фразы на телефоне превращает абзац в лесенку.
            sb.AppendLine();
            sb.AppendLine("Для этого дома я подскажу, кто отвечает за проблему и в какой срок обязан "
                          + "отреагировать — эти правила федеральные и действуют по всей стране.");
            sb.AppendLine();
            sb.AppendLine("Название и телефоны вашей управляющей организации пока не загружены: "
                          + "они появляются при подключении города. Найти их можно в реестре лицензий.");
        }

        if (b.BuildYear is { } year) sb.AppendLine($"Год постройки: {year}");

        // Требование ТЗ: пользователь должен видеть происхождение и дату актуальности данных.
        var source = b.Source == DataSource.TestData
            ? "демонстрационные данные"
            : b.SourceName ?? "официальный источник";
        sb.AppendLine();
        sb.Append($"Источник: {source}");
        if (b.ActualAt is { } actual)
        {
            sb.Append($", на {DateText.Date(new DateTimeOffset(actual.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))}");
        }

        return sb.ToString();
    }

    private async Task<AppUser> EnsureUserAsync(long maxUserId, long chatId, string? name, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.DialogState)
            .FirstOrDefaultAsync(u => u.MaxUserId == maxUserId, ct);

        var now = DateTimeOffset.UtcNow;

        if (user is null)
        {
            user = new AppUser
            {
                MaxUserId = maxUserId,
                MaxChatId = chatId,
                DisplayName = name,
                FirstSeenAt = now,
                LastSeenAt = now
            };
            db.Users.Add(user);
        }
        else
        {
            user.MaxChatId = chatId;
            user.LastSeenAt = now;
            if (!string.IsNullOrWhiteSpace(name)) user.DisplayName = name;
        }

        await db.SaveChangesAsync(ct);
        return user;
    }

    private async Task SetStepAsync(AppUser user, string step, CancellationToken ct,
        string? payloadJson = null)
    {
        var state = user.DialogState ?? await db.DialogStates.FirstOrDefaultAsync(s => s.AppUserId == user.Id, ct);

        if (state is null)
        {
            db.DialogStates.Add(new DialogState
            {
                AppUserId = user.Id,
                Step = step,
                PayloadJson = payloadJson,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            state.Step = step;
            state.PayloadJson = payloadJson;
            state.UpdatedAt = DateTimeOffset.UtcNow;
        }

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
