using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domovoy.Core.Max;

public interface IMaxBotClient
{
    Task<MaxBotInfo?> GetMeAsync(CancellationToken ct = default);

    Task<MaxUpdatesResponse?> GetUpdatesAsync(long? marker, CancellationToken ct = default);

    Task SendMessageAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<object>>? keyboard = null,
        CancellationToken ct = default);

    /// <summary>
    /// Подтверждает нажатие кнопки. API требует непустое тело: нужно передать
    /// либо notification, либо message — иначе возвращает 400.
    /// </summary>
    Task AnswerCallbackAsync(string callbackId, string notification, CancellationToken ct = default);
}

/// <summary>
/// Клиент MAX Bot API. Базовый адрес и авторизация заголовком Authorization без схемы —
/// сверено с client.js официального пакета.
/// </summary>
public sealed class MaxBotClient(
    HttpClient http,
    IOptions<MaxBotOptions> options,
    ILogger<MaxBotClient> logger) : IMaxBotClient
{
    private readonly MaxBotOptions _options = options.Value;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public async Task<MaxBotInfo?> GetMeAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("/me", ct);
        await EnsureSuccessAsync(response, "GET /me", ct);
        return await response.Content.ReadFromJsonAsync<MaxBotInfo>(Json, ct);
    }

    public async Task<MaxUpdatesResponse?> GetUpdatesAsync(long? marker, CancellationToken ct = default)
    {
        var url = $"/updates?limit=100&timeout={_options.PollTimeoutSeconds}";
        if (marker is not null)
        {
            url += $"&marker={marker}";
        }

        using var response = await http.GetAsync(url, ct);

        // 429 и 503 — штатные ситуации: вызывающий цикл просто подождёт и повторит.
        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            logger.LogWarning("GET /updates вернул {Status}, повтор позже", (int)response.StatusCode);
            return null;
        }

        await EnsureSuccessAsync(response, "GET /updates", ct);
        return await response.Content.ReadFromJsonAsync<MaxUpdatesResponse>(Json, ct);
    }

    public async Task SendMessageAsync(long chatId, string text,
        IReadOnlyList<IReadOnlyList<object>>? keyboard = null, CancellationToken ct = default)
    {
        object payload = keyboard is null or { Count: 0 }
            ? new { text }
            : new
            {
                text,
                attachments = new object[]
                {
                    new { type = "inline_keyboard", payload = new { buttons = keyboard } }
                }
            };

        using var response = await http.PostAsJsonAsync($"/messages?chat_id={chatId}", payload, Json, ct);
        await EnsureSuccessAsync(response, "POST /messages", ct);
    }

    public async Task AnswerCallbackAsync(string callbackId, string notification,
        CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"/answers?callback_id={Uri.EscapeDataString(callbackId)}",
            new { notification }, Json, ct);

        await EnsureSuccessAsync(response, "POST /answers", ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string what, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new MaxApiException($"{what} вернул {(int)response.StatusCode}: {body}", response.StatusCode);
    }
}

public sealed class MaxApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
