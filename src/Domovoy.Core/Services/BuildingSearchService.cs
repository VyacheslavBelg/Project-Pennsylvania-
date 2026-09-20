using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Core.Services;

/// <summary>
/// Поиск дома по свободному вводу адреса.
///
/// В MVP ищем по подготовленному набору домов, а не по ФИАС и ГИС ЖКХ: доступа к этим
/// системам нет, и это заявлено в ограничениях. Структура при этом та же, что понадобится
/// для реальной выгрузки, — меняются данные, а не код.
/// </summary>
public sealed class BuildingSearchService(DomovoyDbContext db)
{
    public const int MaxResults = 6;

    public async Task<IReadOnlyList<Building>> SearchAsync(string query, CancellationToken ct = default)
    {
        var tokens = Tokenize(query);
        if (tokens.Count == 0)
        {
            return [];
        }

        var candidates = db.Buildings
            .Include(b => b.Address)
            .Include(b => b.ManagingOrganization)
            .AsQueryable();

        // Каждый введённый фрагмент должен встретиться в нормализованной строке адреса.
        foreach (var token in tokens)
        {
            var t = token;
            candidates = candidates.Where(b => EF.Functions.Like(b.Address.SearchText, $"%{t}%"));
        }

        return await candidates
            .OrderBy(b => b.Address.Street)
            .ThenBy(b => b.Address.House)
            .Take(MaxResults)
            .ToListAsync(ct);
    }

    public Task<Building?> GetAsync(int buildingId, CancellationToken ct = default) =>
        db.Buildings
            .Include(b => b.Address)
            .Include(b => b.ManagingOrganization)
            .FirstOrDefaultAsync(b => b.Id == buildingId, ct);

    /// <summary>
    /// Отбрасываем то, что пользователь пишет по-разному: «ул.», «дом», «д.», запятые.
    /// Иначе «ул. Баумана, д. 15» не найдёт «Баумана 15».
    /// </summary>
    private static List<string> Tokenize(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        string[] noise = ["ул", "улица", "пр", "проспект", "пер", "переулок", "д", "дом", "кв", "г", "город"];

        return query
            .ToLowerInvariant()
            .Replace('ё', 'е')
            .Split([' ', ',', '.', ';', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 0 && !noise.Contains(t))
            .Take(5)
            .ToList();
    }
}
