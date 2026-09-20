namespace Domovoy.Core.Domain;

/// <summary>Управляющая организация или ТСЖ, обслуживающая дом.</summary>
public class ManagingOrganization
{
    public int Id { get; set; }

    public required string Name { get; set; }
    public string? Inn { get; set; }

    public string? Phone { get; set; }

    /// <summary>Телефон аварийно-диспетчерской службы: норматив ответа оператора — 5 минут.</summary>
    public string? EmergencyPhone { get; set; }

    public string? Email { get; set; }
    public string? Website { get; set; }

    public DataSource Source { get; set; }
    public string? SourceName { get; set; }
    public DateOnly? ActualAt { get; set; }
    public string? Territory { get; set; }

    public List<Building> Buildings { get; set; } = [];
}
