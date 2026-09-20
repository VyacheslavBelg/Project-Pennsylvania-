namespace Domovoy.Core.Domain;

/// <summary>Способ управления домом. Влияет на то, кто отвечает за конкретную проблему.</summary>
public enum ManagementKind
{
    Unknown = 0,
    ManagementCompany = 1,
    Hoa = 2,
    Direct = 3
}

/// <summary>
/// Происхождение данных. Требование ТЗ: пользователь должен понимать, что получено из
/// официального источника, что рассчитано продуктом, а что является демонстрационными данными.
/// </summary>
public enum DataSource
{
    TestData = 0,
    Official = 1,
    Calculated = 2,
    Recommendation = 3
}

/// <summary>Роль пользователя по отношению к дому.</summary>
public enum ResidentRole
{
    Resident = 0,
    Owner = 1,
    CouncilChair = 2,
    ManagementStaff = 3
}

/// <summary>Единица измерения нормативного срока: у разных категорий обращений она разная.</summary>
public enum DeadlineUnit
{
    Minutes = 0,
    BusinessDays = 1,
    CalendarDays = 2
}
