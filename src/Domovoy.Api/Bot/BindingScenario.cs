using System.Text;
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
    BuildingSearchService search,
    IMaxBotClient max,
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
        public const string BindReset = "bind:reset";
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

        await ShowEntryPointAsync(user, ct);
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

        // Отвечаем платформе сразу, иначе кнопка остаётся в состоянии ожидания.
        await max.AnswerCallbackAsync(callback.CallbackId, ct: ct);

        if (payload == Callbacks.BindStart || payload == Callbacks.BindReset)
        {
            await SetStepAsync(user, Steps.AwaitingAddress, ct);
            await max.SendMessageAsync(chatId.Value,
                "Введите адрес дома — например, «Баумана 15» или «Ямашева 54».", ct: ct);
            return;
        }

        if (payload.StartsWith(Callbacks.BindPick, StringComparison.Ordinal)
            && int.TryParse(payload[Callbacks.BindPick.Length..], out var buildingId))
        {
            await BindAsync(user, buildingId, ct);
        }
    }

    private async Task ShowEntryPointAsync(AppUser user, CancellationToken ct)
    {
        var link = await db.UserBuildingLinks
            .Include(l => l.Building).ThenInclude(b => b.Address)
            .Include(l => l.Building).ThenInclude(b => b.ManagingOrganization)
            .FirstOrDefaultAsync(l => l.AppUserId == user.Id, ct);

        if (link is null)
        {
            await max.SendMessageAsync(user.MaxChatId,
                "Домовой помогает разобраться, кто отвечает за проблему в доме и в какой срок обязан отреагировать.\n\n"
                + "Начнём с дома — он определяет управляющую организацию и применимые правила.",
                [[MaxButton.Callback("Привязать дом", Callbacks.BindStart)]], ct);
            return;
        }

        List<List<object>> buttons =
        [
            [MaxButton.Callback("Выбрать другой дом", Callbacks.BindReset)]
        ];

        if (!string.IsNullOrWhiteSpace(_options.WebAppUrl))
        {
            buttons.Insert(0, [MaxButton.OpenApp("Открыть карточку дома", _options.WebAppUrl)]);
        }

        await max.SendMessageAsync(user.MaxChatId, DescribeBuilding(link.Building), buttons, ct);
    }

    private async Task SearchAndOfferAsync(AppUser user, string query, CancellationToken ct)
    {
        var found = await search.SearchAsync(query, ct);

        if (found.Count == 0)
        {
            await max.SendMessageAsync(user.MaxChatId,
                "Такой адрес не найден.\n\n"
                + "В демонстрационной версии доступны дома из подготовленного набора по Казани: "
                + "Баумана 15 и 17, Профсоюзная 23, Ямашева 54 и 56, Чистопольская 78, Декабристов 112.",
                ct: ct);
            return;
        }

        var buttons = found
            .Select(b => new List<object>
            {
                MaxButton.Callback($"{b.Address.Street}, д. {b.Address.House}", $"{Callbacks.BindPick}{b.Id}")
            })
            .ToList();

        await max.SendMessageAsync(user.MaxChatId,
            found.Count == 1 ? "Нашёлся один дом. Это он?" : $"Нашлось домов: {found.Count}. Выберите свой.",
            buttons, ct);
    }

    private async Task BindAsync(AppUser user, int buildingId, CancellationToken ct)
    {
        var building = await search.GetAsync(buildingId, ct);
        if (building is null)
        {
            await max.SendMessageAsync(user.MaxChatId, "Этот дом больше недоступен, попробуйте ещё раз.", ct: ct);
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

        List<List<object>> buttons = [];
        if (!string.IsNullOrWhiteSpace(_options.WebAppUrl))
        {
            buttons.Add([MaxButton.OpenApp("Открыть карточку дома", _options.WebAppUrl)]);
        }
        buttons.Add([MaxButton.Callback("Выбрать другой дом", Callbacks.BindReset)]);

        await max.SendMessageAsync(user.MaxChatId, "Дом привязан.\n\n" + DescribeBuilding(building), buttons, ct);
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
        }

        var kind = b.ManagementKind switch
        {
            ManagementKind.ManagementCompany => "управляющая организация",
            ManagementKind.Hoa => "ТСЖ",
            ManagementKind.Direct => "непосредственное управление",
            _ => "не указан"
        };
        sb.AppendLine($"Способ управления: {kind}");

        if (b.BuildYear is { } year) sb.AppendLine($"Год постройки: {year}");

        // Требование ТЗ: пользователь должен видеть происхождение и дату актуальности данных.
        var source = b.Source == DataSource.TestData
            ? "демонстрационные данные"
            : b.SourceName ?? "официальный источник";
        sb.AppendLine();
        sb.Append($"Источник: {source}");
        if (b.ActualAt is { } actual) sb.Append($", на {actual:dd.MM.yyyy}");

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

    private async Task SetStepAsync(AppUser user, string step, CancellationToken ct)
    {
        var state = user.DialogState ?? await db.DialogStates.FirstOrDefaultAsync(s => s.AppUserId == user.Id, ct);

        if (state is null)
        {
            db.DialogStates.Add(new DialogState
            {
                AppUserId = user.Id,
                Step = step,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            state.Step = step;
            state.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
