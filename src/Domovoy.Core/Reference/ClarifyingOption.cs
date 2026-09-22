using Domovoy.Core.Domain;

namespace Domovoy.Core.Reference;

/// <summary>
/// Вариант ответа на уточняющий вопрос категории. Именно он определяет зону
/// ответственности: для большинства проблем это не «или УК, или ресурсник», а граница —
/// внутри квартиры, в общем имуществе дома или до его стены.
///
/// Переменная часть: в разных регионах и у разных управляющих организаций границы
/// проводятся по-своему, поэтому правило живёт в справочнике, а не в коде.
/// </summary>
public class ClarifyingOption
{
    public int Id { get; set; }

    public int ProblemCategoryId { get; set; }
    public ProblemCategory ProblemCategory { get; set; } = null!;

    /// <summary>Текст кнопки, которую видит пользователь.</summary>
    public required string Text { get; set; }

    public int ResponsibilityZoneId { get; set; }
    public ResponsibilityZone ResponsibilityZone { get; set; } = null!;

    /// <summary>Почему ответственность именно такая — показывается вместе с разбором.</summary>
    public string? Explanation { get; set; }

    /// <summary>Правовое основание для границы ответственности.</summary>
    public string? LegalBasis { get; set; }

    /// <summary>Ответ означает аварию: вместо анкеты уводим на телефон диспетчера.</summary>
    public bool IsEmergency { get; set; }

    public int SortOrder { get; set; }

    public DataSource Source { get; set; }
    public string? SourceName { get; set; }
    public DateOnly? ActualAt { get; set; }
    public string? Territory { get; set; }
}
