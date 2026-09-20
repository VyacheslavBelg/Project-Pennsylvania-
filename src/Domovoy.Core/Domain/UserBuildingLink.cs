namespace Domovoy.Core.Domain;

/// <summary>
/// Привязка пользователя к дому. Фундамент всего сценария: без неё неизвестны ни управляющая
/// организация, ни применимые правила.
/// </summary>
public class UserBuildingLink
{
    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public ResidentRole Role { get; set; }

    public string? Apartment { get; set; }

    public DateTimeOffset LinkedAt { get; set; }

    /// <summary>
    /// Подтверждение принадлежности к дому. В MVP привязка принимается на слово — это
    /// заявленное упрощение, а не пропущенная проверка.
    /// </summary>
    public bool IsVerified { get; set; }
}
