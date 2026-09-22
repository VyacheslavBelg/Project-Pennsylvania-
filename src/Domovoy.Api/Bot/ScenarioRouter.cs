using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Max;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Api.Bot;

/// <summary>
/// Разводит события между сценариями: привязка дома и работа с проблемой.
///
/// Роутер знает про MAX, сценарии — про предметную область. Благодаря этому переход
/// с long polling на вебхук не затронет логику: изменится только источник события.
/// </summary>
public sealed class ScenarioRouter(
    DomovoyDbContext db,
    BindingScenario binding,
    ProblemScenario problem,
    IMaxBotClient max,
    ILogger<ScenarioRouter> logger)
{
    public async Task HandleAsync(MaxUpdate update, CancellationToken ct)
    {
        var identity = Identify(update);
        if (identity is null)
        {
            return;
        }

        var (maxUserId, chatId, name) = identity.Value;
        var user = await EnsureUserAsync(maxUserId, chatId, name, ct);

        if (update.UpdateType == MaxUpdate.MessageCallback)
        {
            await HandleCallbackAsync(user, update, ct);
            return;
        }

        var text = update.Message?.Body?.Text?.Trim() ?? string.Empty;
        var step = user.DialogState?.Step ?? string.Empty;

        // Команда всегда сбрасывает контекст: это единственный надёжный способ
        // выбраться из любого состояния.
        if (text.StartsWith('/') || update.UpdateType == MaxUpdate.BotStarted)
        {
            await binding.HandleAsync(update, ct);
            return;
        }

        if (step == ProblemScenario.Steps.DescribingProblem)
        {
            await problem.HandleDescriptionAsync(user, text, ct);
            return;
        }

        await binding.HandleAsync(update, ct);
    }

    private async Task HandleCallbackAsync(AppUser user, MaxUpdate update, CancellationToken ct)
    {
        var callback = update.Callback;
        var payload = callback?.Payload;

        if (callback?.CallbackId is null || payload is null)
        {
            return;
        }

        // Подтверждаем нажатие сразу, но не даём его сбою сорвать действие.
        try
        {
            await max.AnswerCallbackAsync(callback.CallbackId, "Принято", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось подтвердить нажатие кнопки, продолжаем сценарий");
        }

        if (!payload.StartsWith("problem:", StringComparison.Ordinal))
        {
            await binding.HandleAsync(update, ct);
            return;
        }

        if (payload == ProblemScenario.Callbacks.Start)
        {
            await problem.StartAsync(user, ct);
            return;
        }

        if (payload == ProblemScenario.Callbacks.SkipDescription)
        {
            await problem.HandleDescriptionAsync(user, null, ct);
            return;
        }

        if (TryTail(payload, ProblemScenario.Callbacks.Category, out var categoryId))
        {
            await problem.HandleCategoryAsync(user, categoryId, ct);
            return;
        }

        if (TryTail(payload, ProblemScenario.Callbacks.Option, out var optionId))
        {
            await problem.HandleOptionAsync(user, optionId, ct);
            return;
        }

        if (TryTail(payload, ProblemScenario.Callbacks.Submitted, out var submittedId))
        {
            await problem.HandleSubmittedAsync(user, submittedId, ct);
            return;
        }

        if (TryTail(payload, ProblemScenario.Callbacks.Answered, out var answeredId))
        {
            await problem.HandleAnsweredAsync(user, answeredId, ct);
            return;
        }

        if (TryTail(payload, ProblemScenario.Callbacks.Escalate, out var escalateId))
        {
            await problem.HandleEscalateAsync(user, escalateId, ct);
        }
    }

    private static bool TryTail(string payload, string prefix, out int value)
    {
        value = 0;
        return payload.StartsWith(prefix, StringComparison.Ordinal)
               && int.TryParse(payload[prefix.Length..], out value);
    }

    private static (long UserId, long ChatId, string? Name)? Identify(MaxUpdate update)
    {
        if (update.UpdateType == MaxUpdate.MessageCallback)
        {
            var user = update.Callback?.User;
            var chat = update.Message?.Recipient?.ChatId ?? update.ChatId;
            return user is not null && chat is not null ? (user.UserId, chat.Value, user.Name) : null;
        }

        if (update.UpdateType == MaxUpdate.BotStarted && update.ChatId is { } startedChat)
        {
            return (update.User?.UserId ?? 0, startedChat, update.User?.Name);
        }

        var sender = update.Message?.Sender;
        var chatId = update.Message?.Recipient?.ChatId;
        return sender is not null && chatId is not null ? (sender.UserId, chatId.Value, sender.Name) : null;
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
}
