using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Core.Services;

/// <summary>
/// Вариант дома, предложенный пользователю.
/// KnownHouse означает, что у нас есть сведения об управляющей организации;
/// иначе адрес распознан, но региональные данные ещё не загружены.
/// </summary>
public sealed record BuildingCandidate(
    string Display,
    int? BuildingId,
    SuggestedAddress? Suggested,
    string? ManagingOrganizationName)
{
    public bool KnownHouse => BuildingId is not null;
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
    /// Сколько домов предлагать. Столько же отдаёт DaData по умолчанию; больше — уже стена
    /// текста на экране телефона, и тогда полезнее попросить уточнить адрес.
    /// </summary>
    public const int MaxResults = 10;

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
                .Select(b => new BuildingCandidate(
                    b.Address.ToFullString(), b.Id, null, b.ManagingOrganization?.Name))
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

            result.Add(new BuildingCandidate(s.Display, null, s, null));
        }

        // Сохранённые дома, которых нет среди подсказок, — запасной путь: например,
        // когда реестр недоступен или ключ DaData не задан.
        foreach (var b in local)
        {
            if (b.Address.FiasId is { Length: > 0 } fias && seenFias.Contains(fias))
            {
                continue;
            }

            result.Add(new BuildingCandidate(b.Address.ToFullString(), b.Id, null, null));
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
