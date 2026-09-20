namespace Domovoy.Core.Domain;

/// <summary>Многоквартирный дом — центральная сущность дом-ядра.</summary>
public class Building
{
    public int Id { get; set; }

    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    public int? ManagingOrganizationId { get; set; }
    public ManagingOrganization? ManagingOrganization { get; set; }

    public ManagementKind ManagementKind { get; set; }

    public int? BuildYear { get; set; }
    public int? Floors { get; set; }
    public int? Entrances { get; set; }

    public DataSource Source { get; set; }
    public string? SourceName { get; set; }
    public DateOnly? ActualAt { get; set; }
    public string? Territory { get; set; }

    public List<UserBuildingLink> Links { get; set; } = [];
}
