using Domovoy.Core.Reference;

namespace Domovoy.Core.Domain;

public enum RequestStatus
{
    /// <summary>Разбор показан, обращение сформировано, но пользователь его ещё не отправил.</summary>
    Draft = 0,

    /// <summary>Пользователь подтвердил отправку — с этого момента идёт отсчёт норматива.</summary>
    Submitted = 1,

    /// <summary>Пользователь отметил, что получил ответ.</summary>
    Answered = 2,

    /// <summary>Норматив истёк без ответа.</summary>
    Breached = 3,

    /// <summary>Пользователь перешёл к жалобе в инспекцию.</summary>
    Escalated = 4,

    Closed = 5
}

/// <summary>
/// Обращение жителя.
///
/// Продукт не доставляет обращение в систему управляющей организации — канала туда нет.
/// Мы формируем корректный текст, определяем адресата и считаем срок; отправляет житель сам,
/// а отсчёт идёт с момента, когда он подтвердил отправку. Это заявленное ограничение MVP.
/// </summary>
public class Request
{
    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public int ProblemCategoryId { get; set; }
    public ProblemCategory ProblemCategory { get; set; } = null!;

    public int? ClarifyingOptionId { get; set; }
    public ClarifyingOption? ClarifyingOption { get; set; }

    public int? ResponsibilityZoneId { get; set; }
    public ResponsibilityZone? ResponsibilityZone { get; set; }

    /// <summary>Что написал пользователь своими словами.</summary>
    public string? Description { get; set; }

    /// <summary>Сформированный текст обращения.</summary>
    public string? GeneratedText { get; set; }

    public RequestStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Момент, с которого пошёл норматив.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Расчётный срок ответа.</summary>
    public DateTimeOffset? DeadlineAt { get; set; }

    /// <summary>Описание норматива и основание на момент расчёта — чтобы потом не зависеть от справочника.</summary>
    public string? DeadlineDescription { get; set; }
    public string? DeadlineLegalBasis { get; set; }

    /// <summary>Напоминание о приближении срока уже отправлено.</summary>
    public bool ReminderSent { get; set; }

    /// <summary>Уведомление о нарушении срока уже отправлено.</summary>
    public bool BreachNotified { get; set; }
}
