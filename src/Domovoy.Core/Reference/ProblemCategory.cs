using Domovoy.Core.Domain;

namespace Domovoy.Core.Reference;

/// <summary>
/// Категория проблемы. Переменная часть: набор категорий и их формулировки настраиваются
/// под регион и управляющую организацию, код при этом не меняется.
/// </summary>
public class ProblemCategory
{
    public int Id { get; set; }

    public required string Code { get; set; }
    public required string Title { get; set; }

    /// <summary>Вопрос, который бот задаёт для уточнения внутри категории.</summary>
    public string? ClarifyingQuestion { get; set; }

    /// <summary>
    /// Аварийная категория уводит пользователя на телефон АДС вместо формы: при угрозе
    /// имуществу анкета — неподходящий инструмент.
    /// </summary>
    public bool IsEmergency { get; set; }

    public int SortOrder { get; set; }

    public DataSource Source { get; set; }
    public string? SourceName { get; set; }
    public DateOnly? ActualAt { get; set; }
    public string? Territory { get; set; }

    public List<CategoryResponsibility> Responsibilities { get; set; } = [];
    public List<NormativeDeadline> Deadlines { get; set; } = [];
    public List<ClarifyingOption> ClarifyingOptions { get; set; } = [];
}
