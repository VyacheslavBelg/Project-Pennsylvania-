using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Domovoy.Core.Max;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Domovoy.Api.Auth;

/// <summary>Пользователь мини-приложения, подлинность которого подтверждена подписью.</summary>
public sealed record MaxWebAppUser(long UserId, string? Name, string? StartParam);

/// <summary>
/// Проверка initData мини-приложения.
///
/// Мини-приложение открывается на стороннем домене, и всё, что оно присылает, можно
/// подделать. Подлинны только параметры, подписанные платформой: подпись считается
/// от токена бота, которого у клиента нет. Без этой проверки кто угодно мог бы
/// привязать дом от чужого имени.
///
/// Алгоритм — docs/tech/02-max-integration.md, раздел «Валидация данных на бэкенде».
/// </summary>
public sealed class MaxInitDataValidator(
    IOptions<MaxBotOptions> options,
    ILogger<MaxInitDataValidator> logger)
{
    /// <summary>Заголовок, в котором мини-приложение передаёт initData.</summary>
    public const string Header = "X-Max-Init-Data";

    /// <summary>Сколько живут подписанные параметры: защита от повторного использования.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private readonly byte[] _secretKey = HMACSHA256.HashData(
        Encoding.UTF8.GetBytes("WebAppData"),
        Encoding.UTF8.GetBytes(options.Value.Token ?? string.Empty));

    public MaxWebAppUser? Validate(string? initData)
    {
        if (string.IsNullOrWhiteSpace(initData))
        {
            return Reject("initData не передан");
        }

        var pairs = QueryHelpers.ParseQuery(initData);

        if (!pairs.TryGetValue("hash", out var hashValues) || hashValues.ToString() is not { Length: > 0 } hash)
        {
            return Reject("в initData нет подписи");
        }

        // Строка для проверки: все параметры, кроме подписи, по алфавиту, через перевод строки.
        var dataCheck = string.Join('\n', pairs
            .Where(p => p.Key != "hash")
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}"));

        var expected = Convert.ToHexStringLower(HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes(dataCheck)));

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(hash.ToLowerInvariant())))
        {
            return Reject("подпись не сходится");
        }

        if (!pairs.TryGetValue("auth_date", out var authDate)
            || !long.TryParse(authDate, out var seconds)
            || DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(seconds) > MaxAge)
        {
            return Reject("параметры устарели");
        }

        if (!pairs.TryGetValue("user", out var userJson) || ReadUser(userJson.ToString()) is not { } user)
        {
            return Reject("в initData нет пользователя");
        }

        pairs.TryGetValue("start_param", out var startParam);
        return user with { StartParam = startParam.ToString() is { Length: > 0 } s ? s : null };
    }

    private static MaxWebAppUser? ReadUser(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("id", out var id) || !id.TryGetInt64(out var userId))
            {
                return null;
            }

            var first = root.TryGetProperty("first_name", out var f) ? f.GetString() : null;
            var last = root.TryGetProperty("last_name", out var l) ? l.GetString() : null;
            var name = string.Join(' ', new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x)));

            return new MaxWebAppUser(userId, name.Length > 0 ? name : null, null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Причина пишется в лог, а сами параметры — нет: в них персональные данные и подпись.
    private MaxWebAppUser? Reject(string reason)
    {
        logger.LogWarning("initData отклонён: {Reason}", reason);
        return null;
    }
}
