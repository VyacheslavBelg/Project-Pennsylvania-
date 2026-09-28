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
            await SetStepAsync(user, Steps.AwaitingAddress, ct);
            await SendAsync(
                "Введите адрес дома — например, «Баумана 15» или «Ямашева 54».", ct: ct);
            return;
        }

        if (payload.StartsWith(Callbacks.BindPick, StringComparison.Ordinal)
            && int.TryParse(payload[Callbacks.BindPick.Length..], out var buildingId))
        {
            await BindAsync(user, buildingId, ct);
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

        var (common, tails) = SplitCommonPrefix([.. found.Select(c => c.Display)]);

        var sb = new StringBuilder();
        var buttons = new List<List<object>>();
        var suggestedIndex = 0;

        if (found.Count == 1)
        {
            // Нумеровать единственный вариант незачем.
            sb.AppendLine("Нашёлся один дом:");
            sb.AppendLine();
            sb.Append($"🏠 {found[0].Display}");

            buttons.Add([MaxButton.Callback("Да, это мой дом", PayloadFor(found[0], ref suggestedIndex))]);
        }
        else
        {
            sb.Append($"Нашлось домов: {found.Count}");

            // Общая часть адреса выносится наверх: повторять её в каждой строке
            // значит утопить в ней то, чем дома отличаются.
            if (common.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append($"🏠 {common}");
            }

            sb.AppendLine();

            for (var i = 0; i < found.Count; i++)
            {
                sb.AppendLine();
                sb.Append($"{i + 1} — {tails[i]}");
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Нажмите номер своего дома.");

            // Подпись кнопки — только номер: адрес в неё не помещается, а обрезанный
            // выглядит одинаково у соседних корпусов. Номера идут в ряд, чтобы список
            // кнопок не растягивался на пол-экрана.
            const int perRow = 5;

            for (var i = 0; i < found.Count; i += perRow)
            {
                var row = new List<object>();

                for (var j = i; j < Math.Min(i + perRow, found.Count); j++)
                {
                    row.Add(MaxButton.Callback($"{j + 1}", PayloadFor(found[j], ref suggestedIndex)));
                }

                buttons.Add(row);
            }
        }

        if (found.All(c => !c.KnownHouse))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Адрес распознан по государственному адресному реестру. "
                + "Сведений об управляющей организации у нас пока нет.");
        }

        await SendAsync(sb.ToString(), buttons, ct);
    }

    /// <summary>
    /// Дома из своей базы выбираются по идентификатору, распознанные реестром — по позиции
    /// в сохранённом списке подсказок, поэтому счётчик считает только вторые.
    /// </summary>
    private static string PayloadFor(BuildingCandidate candidate, ref int suggestedIndex) =>
        candidate.KnownHouse
            ? $"{Callbacks.BindPick}{candidate.BuildingId}"
            : $"{Callbacks.BindSuggested}{suggestedIndex++}";

    /// <summary>
    /// Убирает общее начало адресов. Когда найдены корпуса одного дома, на кнопке
    /// остаётся «д. 12 к 1»; когда дома в разных городах, общего начала нет
    /// и адрес сокращается уже по длине.
    /// </summary>
    private static (string Common, List<string> Tails) SplitCommonPrefix(List<string> addresses)
    {
        var parts = addresses.Select(a => a.Split(", ")).ToList();
        var common = 0;

        if (parts.Count > 1)
        {
            // Последний сегмент не забираем: без него у кнопки не осталось бы подписи.
            var limit = parts.Min(p => p.Length) - 1;

            while (common < limit
                   && parts.All(p => string.Equals(p[common], parts[0][common], StringComparison.OrdinalIgnoreCase)))
            {
                common++;
            }
        }

        return (string.Join(", ", parts[0].Take(common)),
                [.. parts.Select(p => string.Join(", ", p.Skip(common)))]);
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
        await BindAsync(user, building.Id, ct);
    }

    private async Task BindAsync(AppUser user, int buildingId, CancellationToken ct)
    {
        var building = await db.Buildings
            .Include(b => b.Address)
            .Include(b => b.ManagingOrganization)
            .FirstOrDefaultAsync(b => b.Id == buildingId, ct);

        if (building is null)
        {
            await SendAsync("Этот дом больше недоступен, попробуйте ещё раз.", ct: ct);
            return;
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
    }

    private static string DescribeBuilding(Building b)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🏠 {b.Address}");

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
            sb.AppendLine();
            sb.AppendLine("Для этого дома я подскажу, кто отвечает за проблему и в какой срок");
            sb.AppendLine("обязан отреагировать — эти правила федеральные и действуют по всей стране.");
            sb.AppendLine();
            sb.AppendLine("Название и телефоны вашей управляющей организации пока не загружены:");
            sb.AppendLine("они появляются при подключении города. Найти их можно в реестре лицензий.");
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
