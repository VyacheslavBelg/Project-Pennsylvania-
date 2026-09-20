namespace Domovoy.Core.Domain;

/// <summary>
/// Минимальная телеметрия для показателей из docs/product/04-metrics.md.
/// Текст обращения и другие персональные данные сюда не попадают.
/// </summary>
public class TelemetryEvent
{
    public long Id { get; set; }

    public required string Name { get; set; }

    public long MaxUserId { get; set; }
    public int? BuildingId { get; set; }
    public string? CategoryCode { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
