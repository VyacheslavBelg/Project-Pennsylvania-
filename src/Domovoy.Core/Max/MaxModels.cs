using System.Text.Json.Serialization;

namespace Domovoy.Core.Max;

// Имена полей сверены с типами официального пакета @maxhub/max-bot-api: API использует
// snake_case. Краткое изложение документации выдаёт camelCase и ему доверять нельзя.

public sealed class MaxBotOptions
{
    public const string SectionName = "Max";

    public string Token { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://platform-api2.max.ru";

    /// <summary>Сколько секунд держать long polling. Допустимый диапазон по документации — 0..90.</summary>
    public int PollTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Ник бота. Из него собирается прямая ссылка на мини-приложение
    /// вида https://max.ru/{ник}?startapp={payload}.
    /// </summary>
    public string? BotUsername { get; set; }

    /// <summary>Адрес, по которому раздаётся мини-приложение. Используется для CORS и документации.</summary>
    public string? WebAppUrl { get; set; }

    /// <summary>Готова ли прямая ссылка на мини-приложение.</summary>
    public bool HasMiniApp => !string.IsNullOrWhiteSpace(BotUsername);

    public string MiniAppLink(string payload = "home") =>
        $"https://max.ru/{BotUsername}?startapp={Uri.EscapeDataString(payload)}";
}

public sealed record MaxBotInfo(
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("username")] string? Username);

public sealed record MaxUpdatesResponse(
    [property: JsonPropertyName("updates")] MaxUpdate[]? Updates,
    [property: JsonPropertyName("marker")] long? Marker);

public sealed record MaxUpdate(
    [property: JsonPropertyName("update_type")] string? UpdateType,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("message")] MaxMessage? Message,
    [property: JsonPropertyName("callback")] MaxCallback? Callback,
    [property: JsonPropertyName("chat_id")] long? ChatId,
    [property: JsonPropertyName("user")] MaxUser? User)
{
    public const string MessageCreated = "message_created";
    public const string MessageCallback = "message_callback";
    public const string BotStarted = "bot_started";
}

public sealed record MaxMessage(
    [property: JsonPropertyName("sender")] MaxUser? Sender,
    [property: JsonPropertyName("recipient")] MaxRecipient? Recipient,
    [property: JsonPropertyName("body")] MaxMessageBody? Body);

public sealed record MaxUser(
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("first_name")] string? FirstName);

public sealed record MaxRecipient(
    [property: JsonPropertyName("chat_id")] long? ChatId,
    [property: JsonPropertyName("chat_type")] string? ChatType,
    [property: JsonPropertyName("user_id")] long? UserId);

public sealed record MaxMessageBody(
    [property: JsonPropertyName("mid")] string? Mid,
    [property: JsonPropertyName("seq")] long Seq,
    [property: JsonPropertyName("text")] string? Text);

public sealed record MaxCallback(
    [property: JsonPropertyName("callback_id")] string? CallbackId,
    [property: JsonPropertyName("payload")] string? Payload,
    [property: JsonPropertyName("user")] MaxUser? User);

/// <summary>
/// Кнопки инлайн-клавиатуры. Набор типов взят из keyboard.d.ts официального пакета.
/// </summary>
public static class MaxButton
{
    public static object Callback(string text, string payload) =>
        new { type = "callback", text, payload };

    public static object Link(string text, string url) =>
        new { type = "link", text, url };

    public static object Message(string text) =>
        new { type = "message", text };

    /// <summary>
    /// Кнопка open_app непригодна в нашей конфигурации и оставлена как след проверки.
    ///
    /// Поле web_app обязательно (без него 400 «Field 'webApp' cannot be null»), но при
    /// этом принимает только адреса, зарегистрированные платформой за ботом: наш адрес
    /// на GitHub Pages даёт 404 not.found во всех написаниях. contact_id проблему
    /// не решает.
    ///
    /// Рабочий способ открыть мини-приложение — обычная ссылка на max.ru/{ник}?startapp=,
    /// см. MaxButton.Link и MaxBotOptions.MiniAppLink.
    /// </summary>
    public static object OpenApp(string text, string webApp, string? payload = null) =>
        new { type = "open_app", text, WebApp = webApp, payload };

    /// <summary>Кандидат на платформенный бонус: определение дома по местоположению.</summary>
    public static object RequestGeoLocation(string text, bool quick = true) =>
        new { type = "request_geo_location", text, quick };

    public static object RequestContact(string text) =>
        new { type = "request_contact", text };
}
