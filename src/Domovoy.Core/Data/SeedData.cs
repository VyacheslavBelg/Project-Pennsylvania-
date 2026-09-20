using Domovoy.Core.Domain;
using Domovoy.Core.Reference;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Core.Data;

/// <summary>
/// Начальное наполнение справочников и демонстрационный набор домов.
///
/// Дома и управляющие организации — подготовленные данные, а не выгрузка ГИС ЖКХ: доступа
/// к системе у команды нет. Это помечено в DataSource каждой записи и показывается
/// пользователю в интерфейсе.
///
/// Нормативные сроки взяты из действующих актов, основание хранится в каждой строке.
/// </summary>
public static class SeedData
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    public static async Task ApplyAsync(DomovoyDbContext db, CancellationToken ct = default)
    {
        await SeedZonesAsync(db, ct);
        await SeedCategoriesAsync(db, ct);
        await SeedBuildingsAsync(db, ct);
    }

    private static async Task SeedZonesAsync(DomovoyDbContext db, CancellationToken ct)
    {
        if (await db.ResponsibilityZones.AnyAsync(ct)) return;

        db.ResponsibilityZones.AddRange(
            Zone("uk", "Управляющая организация",
                "Обращение направляется в управляющую организацию вашего дома.",
                "ПП РФ от 15.05.2013 № 416"),
            Zone("rso", "Ресурсоснабжающая организация",
                "За качество ресурса до границы дома отвечает поставщик, а не управляющая организация.",
                "ПП РФ от 06.05.2011 № 354"),
            Zone("contractor", "Подрядчик",
                "Работы выполняет привлечённый подрядчик; обращение подаётся через управляющую организацию как заказчика.",
                "ПП РФ от 15.05.2013 № 416"),
            Zone("municipality", "Муниципальная служба",
                "Вопрос за границей общего имущества дома — обращение в городскую службу.",
                "ЖК РФ"),
            Zone("ads", "Аварийно-диспетчерская служба",
                "Аварийная ситуация: звоните в АДС, норматив ответа оператора — 5 минут.",
                "ПП РФ от 27.03.2018 № 331"));

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCategoriesAsync(DomovoyDbContext db, CancellationToken ct)
    {
        if (await db.ProblemCategories.AnyAsync(ct)) return;

        var zones = await db.ResponsibilityZones.ToDictionaryAsync(z => z.Code, ct);

        var categories = new[]
        {
            Category("leak_emergency", "Протечка или залив прямо сейчас", 10, true,
                "Вода поступает в квартиру в данный момент?"),
            Category("heating", "Холодно в квартире, проблемы с отоплением", 20, false,
                "Батареи холодные во всей квартире или только в части комнат?"),
            Category("hot_water", "Нет горячей воды или она не соответствует нормативу", 30, false,
                "Вода отсутствует полностью или идёт недостаточно горячей?"),
            Category("common_area", "Содержание подъезда, лифта, двора", 40, false,
                "Где именно возникла проблема?"),
            Category("waste", "Вывоз мусора и состояние площадки", 50, false, null),
            Category("billing", "Вопросы по начислениям и тарифам", 60, false, null),
            Category("other", "Другой вопрос по дому", 90, false, null)
        };

        db.ProblemCategories.AddRange(categories);
        await db.SaveChangesAsync(ct);

        var byCode = categories.ToDictionary(c => c.Code);

        AddResponsibility(db, byCode["leak_emergency"], zones["ads"]);
        AddResponsibility(db, byCode["heating"], zones["uk"], ManagementKind.ManagementCompany);
        AddResponsibility(db, byCode["heating"], zones["rso"]);
        AddResponsibility(db, byCode["hot_water"], zones["uk"], ManagementKind.ManagementCompany);
        AddResponsibility(db, byCode["hot_water"], zones["rso"]);
        AddResponsibility(db, byCode["common_area"], zones["uk"]);
        AddResponsibility(db, byCode["waste"], zones["municipality"]);
        AddResponsibility(db, byCode["billing"], zones["uk"]);
        AddResponsibility(db, byCode["other"], zones["uk"]);

        // Сроки различаются на порядки и заданы разными актами — поэтому они в справочнике.
        AddDeadline(db, byCode["leak_emergency"], 5, DeadlineUnit.Minutes,
            "ПП РФ от 27.03.2018 № 331", "Норматив ответа оператора аварийно-диспетчерской службы.");
        AddDeadline(db, byCode["heating"], 3, DeadlineUnit.BusinessDays,
            "ПП РФ от 06.05.2011 № 354", "Обращение по качеству коммунальной услуги.");
        AddDeadline(db, byCode["hot_water"], 3, DeadlineUnit.BusinessDays,
            "ПП РФ от 06.05.2011 № 354", "Обращение по качеству коммунальной услуги.");
        AddDeadline(db, byCode["common_area"], 10, DeadlineUnit.BusinessDays,
            "ПП РФ от 15.05.2013 № 416", "Прочие обращения к управляющей организации.");
        AddDeadline(db, byCode["waste"], 30, DeadlineUnit.CalendarDays,
            "ПП РФ от 15.05.2013 № 416", "Общий порядок рассмотрения обращения.");
        AddDeadline(db, byCode["billing"], 10, DeadlineUnit.BusinessDays,
            "ПП РФ от 15.05.2013 № 416", "Прочие обращения к управляющей организации.");
        AddDeadline(db, byCode["other"], 30, DeadlineUnit.CalendarDays,
            "ПП РФ от 15.05.2013 № 416", "Общий порядок рассмотрения обращения.");

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedBuildingsAsync(DomovoyDbContext db, CancellationToken ct)
    {
        if (await db.Buildings.AnyAsync(ct)) return;

        var uk1 = Organization("ООО УК «Жилищник Вахитовского района»", "1651000001",
            "+7 843 000-00-01", "+7 843 000-00-11");
        var uk2 = Organization("ООО УК «Ново-Савиновская»", "1651000002",
            "+7 843 000-00-02", "+7 843 000-00-22");
        var tsj = Organization("ТСЖ «Декабристов 112»", "1651000003",
            "+7 843 000-00-03", "+7 843 000-00-33");

        db.ManagingOrganizations.AddRange(uk1, uk2, tsj);
        await db.SaveChangesAsync(ct);

        db.Buildings.AddRange(
            BuildingAt("ул. Баумана", "15", uk1, ManagementKind.ManagementCompany, 1968, 9, 4),
            BuildingAt("ул. Баумана", "17", uk1, ManagementKind.ManagementCompany, 1971, 9, 4),
            BuildingAt("ул. Профсоюзная", "23", uk1, ManagementKind.ManagementCompany, 1985, 12, 3),
            BuildingAt("пр. Ямашева", "54", uk2, ManagementKind.ManagementCompany, 1992, 10, 6),
            BuildingAt("пр. Ямашева", "56", uk2, ManagementKind.ManagementCompany, 1994, 10, 6),
            BuildingAt("ул. Чистопольская", "78", uk2, ManagementKind.ManagementCompany, 2008, 17, 2),
            BuildingAt("ул. Декабристов", "112", tsj, ManagementKind.Hoa, 2015, 19, 2));

        await db.SaveChangesAsync(ct);
    }

    private static ResponsibilityZone Zone(string code, string title, string hint, string source) => new()
    {
        Code = code,
        Title = title,
        ActionHint = hint,
        Source = DataSource.Official,
        SourceName = source,
        ActualAt = Today,
        Territory = "РФ"
    };

    private static ProblemCategory Category(
        string code, string title, int sort, bool isEmergency, string? question) => new()
    {
        Code = code,
        Title = title,
        SortOrder = sort,
        IsEmergency = isEmergency,
        ClarifyingQuestion = question,
        Source = DataSource.Official,
        SourceName = "Составлено по ЖК РФ и правилам предоставления коммунальных услуг",
        ActualAt = Today,
        Territory = "РФ"
    };

    private static void AddResponsibility(
        DomovoyDbContext db, ProblemCategory category, ResponsibilityZone zone,
        ManagementKind? appliesTo = null) =>
        db.CategoryResponsibilities.Add(new CategoryResponsibility
        {
            ProblemCategoryId = category.Id,
            ResponsibilityZoneId = zone.Id,
            AppliesToManagement = appliesTo,
            Source = DataSource.Official,
            SourceName = "ПП РФ № 354, ПП РФ № 416",
            ActualAt = Today
        });

    private static void AddDeadline(
        DomovoyDbContext db, ProblemCategory category, int amount, DeadlineUnit unit,
        string legalBasis, string comment) =>
        db.NormativeDeadlines.Add(new NormativeDeadline
        {
            ProblemCategoryId = category.Id,
            Amount = amount,
            Unit = unit,
            LegalBasis = legalBasis,
            Comment = comment,
            Source = DataSource.Official,
            SourceName = legalBasis,
            ActualAt = Today,
            Territory = "РФ"
        });

    private static ManagingOrganization Organization(
        string name, string inn, string phone, string emergencyPhone) => new()
    {
        Name = name,
        Inn = inn,
        Phone = phone,
        EmergencyPhone = emergencyPhone,
        Source = DataSource.TestData,
        SourceName = "Демонстрационные данные",
        ActualAt = Today,
        Territory = "Республика Татарстан"
    };

    private static Building BuildingAt(
        string street, string house, ManagingOrganization org,
        ManagementKind kind, int year, int floors, int entrances) => new()
    {
        Address = new Address
        {
            Region = "Республика Татарстан",
            City = "Казань",
            Street = street,
            House = house,
            SearchText = $"казань {street} {house}".ToLowerInvariant()
        },
        ManagingOrganizationId = org.Id,
        ManagementKind = kind,
        BuildYear = year,
        Floors = floors,
        Entrances = entrances,
        Source = DataSource.TestData,
        SourceName = "Демонстрационные данные",
        ActualAt = Today,
        Territory = "Республика Татарстан"
    };
}
