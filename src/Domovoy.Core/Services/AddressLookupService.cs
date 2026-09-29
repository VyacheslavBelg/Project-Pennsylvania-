using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Core.Services;

/// <summary>
/// Вариант дома, предложенный пользователю.
/// KnownHouse означает, что у нас есть сведения об управляющей организации;
/// иначе адрес распознан, но региональные данные ещё не загружены.
/// </summary>
/// <param name="Locality">Населённый пункт с регионом — заголовок группы в списке.</param>
/// <param name="Line">Улица и дом внутри населённого пункта.</param>
public sealed record BuildingCandidate(
    string Display,
    string Locality,
    string Line,
    int? BuildingId,
    SuggestedAddress? Suggested,
    string? ManagingOrganizationName)
{
    public bool KnownHouse => BuildingId is not null;

    public static BuildingCandidate FromBuilding(Building b, string? managingOrganization) =>
        new(b.Address.ToFullString(), b.Address.Locality, b.Address.StreetLine,
            b.Id, null, managingOrganization);

    /// <summary>
    /// Подсказка реестра. Населённый пункт выделяется из готовой строки, а не собирается
    /// из полей: в строке реестра есть тип строения — «зд 12» и «д 12» разные дома —
    /// и район города, которых в разобранных полях нет.
    /// </summary>
    public static BuildingCandidate FromSuggestion(SuggestedAddress s)
    {
        var (locality, line) = SplitAtLocality(s.Display, s.City, s.Region);
        return new BuildingCandidate(s.Display, locality, line, null, s, null);
    }

    private static (string Locality, string Line) SplitAtLocality(string display, string? city, string? region)
    {
        if (city is { Length: > 0 })
        {
            // «…, г Новокузнецк, р-н Заводской, ул Климасенко, зд 12»
            var marker = $", {city}, ";
            var at = display.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                return (display[..(at + marker.Length - 2)], display[(at + marker.Length)..]);
            }

            // Город федерального значения стоит первым: «г Москва, ул Тверская, д 13».
            if (display.StartsWith($"{city}, ", StringComparison.OrdinalIgnoreCase))
            {
                return (city, display[(city.Length + 2)..]);
            }
        }

        if (region is { Length: > 0 } && display.StartsWith($"{region}, ", StringComparison.OrdinalIgnoreCase))
        {
            return (region, display[(region.Length + 2)..]);
        }

        return (string.Empty, display);
    }
}

/// <summary>
/// Поиск дома: сначала по своей базе, затем по государственному адресному реестру.
///
/// Разделение намеренное и совпадает с границей «ядро / переменная часть»: адрес
/// распознаётся для любого дома России, а сведения об управляющей организации
/// появляются только там, где регион подключён.
/// </summary>
public sealed class AddressLookupService(
    DomovoyDbContext db,
    BuildingSearchService localSearch,
    IAddressSuggestService suggest)
{
    /// <summary>
    /// Сколько домов предлагать: столько, сколько реестр отдаёт за один запрос. Больше
    /// DaData не возвращает, и дальше вариантов можно только сузить — городом или корпусом.
    /// </summary>
    public const int MaxResults = 20;

    public async Task<IReadOnlyList<BuildingCandidate>> FindAsync(
        string query, CancellationToken ct = default)
    {
        var local = await localSearch.SearchAsync(query, ct);

        // Дома с известной управляющей организацией — подготовленные данные. Они полнее
        // реестра, поэтому вытесняют подсказки.
        var curated = local.Where(b => b.ManagingOrganizationId is not null).ToList();
        if (curated.Count > 0)
        {
            return curated
                .Select(b => BuildingCandidate.FromBuilding(b, b.ManagingOrganization?.Name))
                .ToList();
        }

        var suggested = await suggest.SuggestAsync(query, MaxResults, ct);

        // Подсказки реестра — первыми и в его порядке: он ранжирует по релевантности,
        // и его сведения полнее сохранённых у нас. Раньше первыми шли дома из прошлых
        // выборов, в том числе записанные без корпуса, и в списке оказывались две
        // одинаковые строки. Дом, который у нас уже есть, выбирается из подсказки
        // и при выборе дополняется из неё.
        var result = new List<BuildingCandidate>();
        var seenFias = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in suggested)
        {
            if (s.FiasId is { Length: > 0 } fias && !seenFias.Add(fias))
            {
                continue;
            }

            result.Add(BuildingCandidate.FromSuggestion(s));
        }

        // Сохранённые дома, которых нет среди подсказок, — запасной путь: например,
        // когда реестр недоступен или ключ DaData не задан.
        foreach (var b in local)
        {
            if (b.Address.FiasId is { Length: > 0 } fias && seenFias.Contains(fias))
            {
                continue;
            }

            result.Add(BuildingCandidate.FromBuilding(b, null));
        }

        return result.Take(MaxResults).ToList();
    }

    /// <summary>
    /// Сохраняет дом, распознанный по адресному реестру. Управляющая организация остаётся
    /// неизвестной: эти сведения загружаются при подключении региона.
    /// </summary>
    public async Task<Building> EnsureBuildingAsync(SuggestedAddress address, CancellationToken ct = default)
    {
        if (address.FiasId is { Length: > 0 } fias)
        {
            var existing = await db.Buildings
                .Include(b => b.Address)
                .Include(b => b.ManagingOrganization)
                .FirstOrDefaultAsync(b => b.Address.FiasId == fias, ct);

            if (existing is not null)
            {
                // Дом мог быть сохранён до того, как мы научились забирать корпус.
                // Реестр здесь источник истины, поэтому дописываем молча.
                if (existing.Address.Block is null or { Length: 0 }
                    && address.Block is { Length: > 0 })
                {
                    existing.Address.Block = address.Block;
                    await db.SaveChangesAsync(ct);
                }

                return existing;
            }
        }

        var building = new Building
        {
            Address = new Address
            {
                Region = address.Region ?? string.Empty,
                City = address.City ?? string.Empty,
                Street = address.Street ?? string.Empty,
                House = address.House ?? string.Empty,
                Block = address.Block,
                FiasId = address.FiasId,
                SearchText = address.Display.ToLowerInvariant()
            },
            ManagementKind = ManagementKind.Unknown,
            Source = DataSource.Official,
            SourceName = "Государственный адресный реестр (ФИАС), через DaData",
            ActualAt = DateOnly.FromDateTime(DateTime.UtcNow),
            Territory = address.Region
        };

        db.Buildings.Add(building);
        await db.SaveChangesAsync(ct);

        return building;
    }
}
