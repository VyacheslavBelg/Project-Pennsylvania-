using Domovoy.Core.Domain;

namespace Domovoy.Core.Reference;

/// <summary>
/// Нормативный срок реакции по категории обращения. Сроки заданы разными актами и
/// различаются на порядки — от 5 минут для аварийной службы до 30 календарных дней.
/// </summary>
public class NormativeDeadline
{
    public int Id { get; set; }

    public int ProblemCategoryId { get; set; }
    public ProblemCategory ProblemCategory { get; set; } = null!;

    public int Amount { get; set; }
    public DeadlineUnit Unit { get; set; }

    /// <summary>Нормативное основание — показывается пользователю вместе со сроком.</summary>
    public required string LegalBasis { get; set; }

    public string? Comment { get; set; }

    public DataSource Source { get; set; }
    public string? SourceName { get; set; }
    public DateOnly? ActualAt { get; set; }
    public string? Territory { get; set; }

    public string Describe() => Unit switch
    {
        DeadlineUnit.Minutes => $"{Amount} мин.",
        DeadlineUnit.BusinessDays => $"{Amount} раб. дн.",
        DeadlineUnit.CalendarDays => $"{Amount} кал. дн.",
        _ => Amount.ToString()
    };
}
