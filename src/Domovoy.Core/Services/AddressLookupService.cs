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
    public async Task<IReadOnlyList<BuildingCandidate>> FindAsync(
        string query, CancellationToken ct = default)
    {
        var local = await localSearch.SearchAsync(query, ct);

        var candidates = local
            .Select(b => new BuildingCandidate(
                b.Address.ToString(), b.Id, null, b.ManagingOrganization?.Name))
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates;
        }

        // Своих данных нет — спрашиваем адресный реестр.
        var suggested = await suggest.SuggestAsync(query, ct);

        return suggested
            .Select(s => new BuildingCandidate(s.Display, null, s, null))
            .ToList();
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
