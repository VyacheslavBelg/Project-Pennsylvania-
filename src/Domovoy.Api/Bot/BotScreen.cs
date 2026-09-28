using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Max;

namespace Domovoy.Api.Bot;

/// <summary>
/// Один живой экран бота в чате.
///
/// Нажатие кнопки превращает в следующий экран то самое сообщение, на котором она нажата:
/// ответ на нажатие в MAX умеет заменять сообщение. На текст пользователя бот отвечает
/// новым сообщением, а прежний экран удаляет. В чате остаются актуальный экран и сообщения
/// самого пользователя — листать вниз в поисках нужных кнопок не приходится.
///
/// Живёт в пределах обработки одного события.
/// </summary>
public sealed class BotScreen(
    IMaxBotClient max,
    DomovoyDbContext db,
    ILogger<BotScreen> logger)
{
    private AppUser? _user;
    private string? _callbackId;
    private string? _callbackMessageId;

    /// <summary>Начало обработки события. Для нажатия кнопки — её идентификатор и сообщение.</summary>
    public void Begin(AppUser user, string? callbackId, string? callbackMessageId)
    {
        _user = user;
        _callbackId = callbackId;
        _callbackMessageId = callbackMessageId;
    }

    public async Task ShowAsync(string text, IReadOnlyList<IReadOnlyList<object>>? keyboard,
        CancellationToken ct)
    {
        var user = _user ?? throw new InvalidOperationException("Экран показан до начала обработки события.");

        // Нажатие кнопки: следующим экраном становится то сообщение, где она нажата.
        if (_callbackId is { } callbackId)
        {
            _callbackId = null;

            if (await TryReplaceAsync(callbackId, text, keyboard, ct))
            {
                await RememberAsync(user, _callbackMessageId, ct);
                return;
            }
        }

        var sent = await max.SendMessageAsync(user.MaxChatId, text, keyboard, ct);
        await RememberAsync(user, sent, ct);
    }

    /// <summary>
    /// Конец обработки. Если обработчик ничего не показал, нажатие всё равно нужно
    /// подтвердить — иначе кнопка так и крутит индикатор загрузки.
    /// </summary>
    public async Task CompleteAsync(CancellationToken ct)
    {
        if (_callbackId is not { } callbackId)
        {
            return;
        }

        _callbackId = null;

        try
        {
            await max.AnswerCallbackAsync(callbackId, "Принято", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось подтвердить нажатие кнопки");
        }
    }

    private async Task<bool> TryReplaceAsync(string callbackId, string text,
        IReadOnlyList<IReadOnlyList<object>>? keyboard, CancellationToken ct)
    {
        try
        {
            return await max.ReplaceOnCallbackAsync(callbackId, text, keyboard, ct);
        }
        catch (Exception ex)
        {
            // Замена — удобство, а не условие работы: при отказе экран уйдёт новым сообщением.
            logger.LogWarning(ex, "Не удалось заменить сообщение по нажатию, отправляю новое");
            return false;
        }
    }

    private async Task RememberAsync(AppUser user, string? current, CancellationToken ct)
    {
        var previous = user.LastBotMessageId;

        // Прежний экран убирается, только если это другое сообщение. Нажатие кнопки
        // в старом сообщении делает живым его, а не тот экран, что был внизу.
        if (previous is { Length: > 0 } && previous != current)
        {
            try
            {
                await max.DeleteMessageAsync(previous, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось удалить прежний экран {Message}", previous);
            }
        }

        user.LastBotMessageId = current;
        await db.SaveChangesAsync(ct);
    }
}
