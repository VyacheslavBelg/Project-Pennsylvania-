using Domovoy.Core.Max;

namespace Domovoy.Api.Bot;

/// <summary>
/// Получение событий через long polling.
///
/// Документация MAX рекомендует для прода вебхук, но он требует публичного HTTPS-адреса,
/// а хостинг ещё не поднят. Сценарий принимает готовый MaxUpdate и не знает, откуда тот
/// пришёл, поэтому переход на вебхук будет добавлением эндпоинта, а не переделкой логики.
///
/// Важно: одновременно опрашивать события может только один процесс — два экземпляра
/// с одним токеном начнут перехватывать сообщения друг у друга.
/// </summary>
public sealed class LongPollingService(
    IServiceProvider services,
    IMaxBotClient max,
    ILogger<LongPollingService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForBotAsync(stoppingToken);

        long? marker = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await max.GetUpdatesAsync(marker, stoppingToken);

                if (batch is null)
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                    continue;
                }

                marker = batch.Marker ?? marker;

                foreach (var update in batch.Updates ?? [])
                {
                    await DispatchAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Сбой цикла опроса, повтор через {Delay} с", RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Каждое событие обрабатывается в своей области: у сценария есть DbContext,
    /// и ошибка на одном сообщении не должна ронять цикл или портить состояние соседнего.
    /// </summary>
    private async Task DispatchAsync(MaxUpdate update, CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<ScenarioRouter>();
            await router.HandleAsync(update, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось обработать событие {Type}", update.UpdateType);
        }
    }

    private async Task WaitForBotAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var me = await max.GetMeAsync(ct);
                logger.LogInformation("Бот подключён: {Name} (@{Username}), user_id={Id}",
                    me?.Name, me?.Username, me?.UserId);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (HttpRequestException ex) when (ex.InnerException is System.Security.Authentication.AuthenticationException)
            {
                logger.LogError(
                    "Не удалось установить TLS-соединение с MAX API. В образе нет корневого "
                    + "сертификата НУЦ Минцифры — проверьте, что docker/certs скопированы и "
                    + "выполнен update-ca-certificates.");
                await Task.Delay(RetryDelay, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MAX API недоступен, повтор через {Delay} с", RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }
}
