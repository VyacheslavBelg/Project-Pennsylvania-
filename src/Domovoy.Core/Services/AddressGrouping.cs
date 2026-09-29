namespace Domovoy.Core.Services;

/// <summary>Строка списка: номер варианта в исходной выдаче и его подпись внутри группы.</summary>
public sealed record AddressGroupItem(int Index, string Label);

/// <summary>Дома одного населённого пункта под общим заголовком.</summary>
public sealed record AddressGroup(string Header, IReadOnlyList<AddressGroupItem> Items);

/// <summary>
/// Раскладка найденных домов для показа.
///
/// Дома группируются по населённому пункту, а внутри группы общая часть адреса уходит
/// в заголовок: у корпусов одного дома различается только хвост, и повторять область,
/// город и улицу в каждой строке значит утопить в них то, чем дома отличаются.
/// Одна раскладка на бот и мини-приложение — выглядят они одинаково.
/// </summary>
public static class AddressGrouping
{
    public static IReadOnlyList<AddressGroup> Group(IReadOnlyList<BuildingCandidate> candidates) =>
        candidates
            .Select((candidate, index) => (candidate, index))
            // Порядок групп — по первому появлению: реестр ранжирует по релевантности.
            .GroupBy(x => x.candidate.Locality, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var items = g.ToList();

                // Единственный дом в населённом пункте заголовка не получает: у «Карбышева 22»
                // такие группы во всех двадцати городах, и вместо списка вышла бы лестница
                // из заголовков. Адрес показывается строкой целиком.
                if (items.Count == 1)
                {
                    var only = items[0];
                    var full = string.Join(", ", new[] { g.Key, only.candidate.Line }.Where(p => p.Length > 0));
                    return new AddressGroup(string.Empty, [new AddressGroupItem(only.index, full)]);
                }

                var (common, tails) = SplitCommonPrefix([.. items.Select(x => x.candidate.Line)]);
                var header = string.Join(", ", new[] { g.Key, common }.Where(p => p.Length > 0));

                return new AddressGroup(header,
                    [.. items.Select((x, i) => new AddressGroupItem(x.index, tails[i]))]);
            })
            .ToList();

    /// <summary>
    /// Общее начало адресов по сегментам через запятую. У единственного адреса общей
    /// части нет, и последний сегмент не забирается никогда — иначе строке не осталось
    /// бы подписи.
    /// </summary>
    public static (string Common, List<string> Tails) SplitCommonPrefix(IReadOnlyList<string> addresses)
    {
        var parts = addresses.Select(a => a.Split(", ")).ToList();
        var common = 0;

        if (parts.Count > 1)
        {
            var limit = parts.Min(p => p.Length) - 1;

            while (common < limit
                   && parts.All(p => string.Equals(p[common], parts[0][common], StringComparison.OrdinalIgnoreCase)))
            {
                common++;
            }
        }

        return (parts.Count > 0 ? string.Join(", ", parts[0].Take(common)) : string.Empty,
                [.. parts.Select(p => string.Join(", ", p.Skip(common)))]);
    }
}
