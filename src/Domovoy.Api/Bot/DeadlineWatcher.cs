using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Max;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Api.Bot;

/// <summary>
/// Следит за нормативными сроками поданных обращений.
///
/// Это вторая половина ценности продукта. По отзывам на существующие сервисы главная
/// претензия жителей — «исполнение есть, а работы по факту нет»: человек подал обращение
/// и не знает, что делать дальше. Здесь срок отслеживается сам, а при нарушении
/// пользователь сразу получает готовый пакет для жалобы в инспекцию.
/// </summary>
public sealed class DeadlineWatcher(
    IServiceProvider services,
    ILogger<DeadlineWatcher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>За сколько до истечения предупреждать.</summary>
    private static readonly TimeSpan ReminderLead = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Сбой проверки сроков");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomovoyDbContext>();
        var max = scope.ServiceProvider.GetRequiredService<IMaxBotClient>();

        var now = DateTimeOffset.UtcNow;

        var pending = await db.Requests
            .Include(r => r.AppUser)
            .Include(r => r.ProblemCategory)
            .Where(r => r.Status == RequestStatus.Submitted && r.DeadlineAt != null)
            .Where(r => !r.BreachNotified)
            .ToListAsync(ct);

        foreach (var request in pending)
        {
            var deadline = request.DeadlineAt!.Value;

            if (now >= deadline)
            {
                await NotifyBreachAsync(max, request, ct);
                request.Status = RequestStatus.Breached;
                request.BreachNotified = true;

                db.TelemetryEvents.Add(new TelemetryEvent
                {
                    Name = "deadline_breached",
                    MaxUserId = request.AppUser.MaxUserId,
                    BuildingId = request.BuildingId,
                    CategoryCode = request.ProblemCategory.Code,
                    OccurredAt = now
                });

                continue;
            }

            if (!request.ReminderSent && deadline - now <= ReminderLead)
            {
                await NotifyUpcomingAsync(max, request, deadline, ct);
                request.ReminderSent = true;
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task NotifyUpcomingAsync(
        IMaxBotClient max, Request request, DateTimeOffset deadline, CancellationToken ct)
    {
        try
        {
            await max.SendMessageAsync(request.AppUser.MaxChatId,
                $"Напоминание: срок ответа по обращению «{request.ProblemCategory.Title}» "
                + $"истекает {deadline.ToLocalTime():dd.MM.yyyy HH:mm}.\n\n"
                + "Если ответ уже получен — отметьте это, чтобы я не беспокоил.",
                [
                    [MaxButton.Callback("Мне уже ответили", $"{ProblemScenario.Callbacks.Answered}{request.Id}")]
                ], ct);
        }
        catch (Exception ex)
        {
            // Недоставленное напоминание не должно останавливать обработку остальных.
            logger.LogWarning(ex, "Не удалось отправить напоминание по обращению {Request}", request.Id);
        }
    }

    private async Task NotifyBreachAsync(IMaxBotClient max, Request request, CancellationToken ct)
    {
        try
        {
            await max.SendMessageAsync(request.AppUser.MaxChatId,
                $"Срок по обращению «{request.ProblemCategory.Title}» истёк "
                + $"{request.DeadlineAt!.Value.ToLocalTime():dd.MM.yyyy HH:mm}, ответа нет.\n\n"
                + $"Норматив: {request.DeadlineDescription}, основание: {request.DeadlineLegalBasis}.\n\n"
                + "Это основание для жалобы в жилищную инспекцию. Могу собрать готовый текст "
                + "с датами и нормами — останется только отправить.",
                [
                    [MaxButton.Callback("Собрать жалобу в инспекцию", $"{ProblemScenario.Callbacks.Escalate}{request.Id}")],
                    [MaxButton.Callback("Мне уже ответили", $"{ProblemScenario.Callbacks.Answered}{request.Id}")]
                ], ct);

            logger.LogInformation("Срок по обращению {Request} нарушен, пользователь уведомлён", request.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось уведомить о нарушении срока по обращению {Request}", request.Id);
        }
    }
}
