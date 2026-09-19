using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Проверка связи с MAX Bot API: бот отвечает «Hello, World!» на любое сообщение.
// Работает через long polling, поэтому публичный адрес и хостинг не нужны.
// Для прода вместо этого регистрируется вебхук — POST /subscriptions.

const string BaseUrl = "https://platform-api2.max.ru";

var token = Environment.GetEnvironmentVariable("MAX_BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Не задана переменная окружения MAX_BOT_TOKEN.");
    Console.Error.WriteLine("PowerShell:  $env:MAX_BOT_TOKEN = \"<токен>\"");
    Console.Error.WriteLine("Git Bash:    export MAX_BOT_TOKEN='<токен>'");
    return 1;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
http.DefaultRequestHeaders.Add("Authorization", token);

var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

// 1. Проверяем токен и заодно узнаём фактический ник бота.
try
{
    using var meResponse = await http.GetAsync($"{BaseUrl}/me");
    var meBody = await meResponse.Content.ReadAsStringAsync();

    if (meResponse.StatusCode == HttpStatusCode.Unauthorized)
    {
        Console.Error.WriteLine("401: токен не принят. Проверьте значение MAX_BOT_TOKEN.");
        return 1;
    }

    if (!meResponse.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"GET /me вернул {(int)meResponse.StatusCode}: {meBody}");
        return 1;
    }

    var me = JsonSerializer.Deserialize<BotInfo>(meBody, json);
    Console.WriteLine($"Бот: {me?.Name} (@{me?.Username}), user_id={me?.UserId}");
    Console.WriteLine("Напишите боту в MAX — он ответит «Hello, World!». Ctrl+C для выхода.");
    Console.WriteLine();
}
catch (HttpRequestException ex) when (HasCertificateProblem(ex))
{
    Console.Error.WriteLine("Не удалось установить TLS-соединение с platform-api2.max.ru.");
    Console.Error.WriteLine("Сертификат MAX выпущен НУЦ Минцифры, и его корень не входит");
    Console.Error.WriteLine("в стандартное хранилище Windows.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Установите корневой сертификат: https://www.gosuslugi.ru/crt");
    return 1;
}

// 2. Long polling: тянем события и отвечаем на каждое входящее сообщение.
long? marker = null;

while (true)
{
    try
    {
        var url = $"{BaseUrl}/updates?limit=100&timeout=30";
        if (marker is not null)
        {
            url += $"&marker={marker}";
        }

        using var response = await http.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // 429 — превышен лимит в 30 запросов в секунду, 503 — сервис недоступен.
            Console.Error.WriteLine($"GET /updates вернул {(int)response.StatusCode}: {body}");
            await Task.Delay(TimeSpan.FromSeconds(5));
            continue;
        }

        var batch = JsonSerializer.Deserialize<UpdatesResponse>(body, json);
        marker = batch?.Marker ?? marker;

        foreach (var update in batch?.Updates ?? [])
        {
            if (update.UpdateType != "message_created")
            {
                continue;
            }

            var chatId = update.Message?.Recipient?.ChatId;
            if (chatId is null)
            {
                continue;
            }

            var incoming = update.Message?.Body?.Text ?? "<без текста>";
            var from = update.Message?.Sender?.Name ?? "неизвестный";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {from}: {incoming}");

            await SendMessageAsync(chatId.Value, "Hello, World!");
        }
    }
    catch (TaskCanceledException)
    {
        // Штатное завершение long polling по таймауту — просто опрашиваем снова.
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Ошибка цикла опроса: {ex.Message}");
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}

async Task SendMessageAsync(long chatId, string text)
{
    var payload = JsonSerializer.Serialize(new { text });
    using var content = new StringContent(payload, Encoding.UTF8, "application/json");

    using var response = await http.PostAsync($"{BaseUrl}/messages?chat_id={chatId}", content);

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.Error.WriteLine($"POST /messages вернул {(int)response.StatusCode}: {error}");
        return;
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] -> Hello, World!");
}

static bool HasCertificateProblem(Exception ex)
{
    for (var e = ex; e is not null; e = e.InnerException)
    {
        if (e is AuthenticationException)
        {
            return true;
        }
    }

    return false;
}

sealed record BotInfo(
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("username")] string? Username);

sealed record UpdatesResponse(
    [property: JsonPropertyName("updates")] Update[]? Updates,
    [property: JsonPropertyName("marker")] long? Marker);

sealed record Update(
    [property: JsonPropertyName("update_type")] string? UpdateType,
    [property: JsonPropertyName("message")] MaxMessage? Message);

sealed record MaxMessage(
    [property: JsonPropertyName("sender")] Sender? Sender,
    [property: JsonPropertyName("recipient")] Recipient? Recipient,
    [property: JsonPropertyName("body")] MessageBody? Body);

sealed record Sender(
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("name")] string? Name);

sealed record Recipient(
    [property: JsonPropertyName("chat_id")] long? ChatId,
    [property: JsonPropertyName("chat_type")] string? ChatType);

sealed record MessageBody(
    [property: JsonPropertyName("mid")] string? Mid,
    [property: JsonPropertyName("text")] string? Text);
