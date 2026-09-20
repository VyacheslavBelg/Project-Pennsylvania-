using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domovoy.Core.Services;

public sealed class DaDataOptions
{
    public const string SectionName = "DaData";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://suggestions.dadata.ru/suggestions/api/4_1/rs/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>Адрес, распознанный по государственному адресному реестру.</summary>
public sealed record SuggestedAddress(
    string Display,
    string? FiasId,
    string? Region,
    string? City,
    string? Street,
    string? House);

public interface IAddressSuggestService
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<SuggestedAddress>> SuggestAsync(string query, CancellationToken ct = default);
}

/// <summary>
/// Подсказки по адресам через DaData, источник данных — ФИАС/ГАР.
///
/// Нужен, чтобы продукт понимал любой адрес России, а не только подготовленный набор.
/// Управляющую организацию это не даёт: связка «дом → УК» живёт в ГИС ЖКХ, доступ к которой
/// выдаётся только организациям. Поэтому адрес — ядро, а сведения об УК — переменная часть,
/// загружаемая при подключении региона.
/// </summary>
public sealed class DaDataAddressSuggestService(
    HttpClient http,
    IOptions<DaDataOptions> options,
    ILogger<DaDataAddressSuggestService> logger) : IAddressSuggestService
{
    private readonly DaDataOptions _options = options.Value;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsConfigured => _options.IsConfigured;

    public async Task<IReadOnlyList<SuggestedAddress>> SuggestAsync(
        string query, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        // from_bound/to_bound = house: интересуют только результаты до уровня дома,
        // иначе в подсказках окажутся города и улицы, к которым нельзя привязаться.
        var request = new
        {
            query,
            count = 5,
            from_bound = new { value = "house" },
            to_bound = new { value = "house" }
        };

        // Наблюдался разовый обрыв TLS-рукопожатия. Одна повторная попытка дешевле,
        // чем молча сузить пользователю список домов до локальных данных.
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var response = await http.PostAsJsonAsync("suggest/address", request, Json, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("DaData вернула {Status}: {Body}", (int)response.StatusCode, body);
                    return [];
                }

                var payload = await response.Content.ReadFromJsonAsync<DaDataResponse>(Json, ct);

                return (payload?.Suggestions ?? [])
                    .Where(s => s.Data?.House is not null)
                    .Select(s => new SuggestedAddress(
                        s.Value ?? string.Empty,
                        s.Data?.FiasId,
                        s.Data?.RegionWithType,
                        s.Data?.CityWithType ?? s.Data?.SettlementWithType,
                        s.Data?.StreetWithType,
                        s.Data?.House))
                    .ToList();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt == 1)
            {
                logger.LogWarning(ex, "Подсказки по адресу недоступны, повторяю");
            }
            catch (Exception ex)
            {
                // Недоступность подсказок не ломает сценарий: остаётся поиск по своей базе.
                logger.LogWarning(ex, "Не удалось получить подсказки по адресу");
            }
        }

        return [];
    }

    private sealed record DaDataResponse(
        [property: JsonPropertyName("suggestions")] DaDataSuggestion[]? Suggestions);

    private sealed record DaDataSuggestion(
        [property: JsonPropertyName("value")] string? Value,
        [property: JsonPropertyName("data")] DaDataAddress? Data);

    private sealed record DaDataAddress(
        [property: JsonPropertyName("fias_id")] string? FiasId,
        [property: JsonPropertyName("region_with_type")] string? RegionWithType,
        [property: JsonPropertyName("city_with_type")] string? CityWithType,
        [property: JsonPropertyName("settlement_with_type")] string? SettlementWithType,
        [property: JsonPropertyName("street_with_type")] string? StreetWithType,
        [property: JsonPropertyName("house")] string? House);
}
