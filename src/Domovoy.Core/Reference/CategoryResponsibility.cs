using Domovoy.Core.Domain;

namespace Domovoy.Core.Reference;

/// <summary>
/// Сопоставление категории проблемы и зоны ответственности. Может зависеть от способа
/// управления домом и от территории — поэтому это строка справочника, а не условие в коде.
/// </summary>
public class CategoryResponsibility
{
    public int Id { get; set; }

    public int ProblemCategoryId { get; set; }
    public ProblemCategory ProblemCategory { get; set; } = null!;

    public int ResponsibilityZoneId { get; set; }
    public ResponsibilityZone ResponsibilityZone { get; set; } = null!;

    /// <summary>Если задано — правило применяется только к домам с таким способом управления.</summary>
    public ManagementKind? AppliesToManagement { get; set; }

    /// <summary>Если задано — правило действует только на этой территории.</summary>
    public string? Territory { get; set; }

    public DataSource Source { get; set; }
    public string? SourceName { get; set; }
    public DateOnly? ActualAt { get; set; }
}
