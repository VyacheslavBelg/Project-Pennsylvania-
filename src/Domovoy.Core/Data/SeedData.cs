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

    /// <summary>
    /// Дата сверки справочника проблем с источниками — по каталогу
    /// docs/product/08-problem-catalog.md. Демонстрационные дома готовились раньше
    /// и сохраняют свою дату.
    /// </summary>
    public static readonly DateOnly CatalogDate = new(2026, 9, 26);

    public static async Task ApplyAsync(DomovoyDbContext db, CancellationToken ct = default)
    {
        await SeedZonesAsync(db, ct);
        await SeedCategoriesAsync(db, ct);
        await SeedClarifyingOptions.ApplyAsync(db, ct);
        await SeedBuildingsAsync(db, ct);
    }

    private static async Task SeedZonesAsync(DomovoyDbContext db, CancellationToken ct)
    {
        if (await db.ResponsibilityZones.AnyAsync(ct)) return;

        db.ResponsibilityZones.AddRange(
            Zone("uk", "Управляющая организация", "В управляющую организацию",
                "Обращение направляется в управляющую организацию вашего дома.",
                "ПП РФ от 15.05.2013 № 416"),
            Zone("rso", "Ресурсоснабжающая организация", "В ресурсоснабжающую организацию",
                "За качество ресурса до границы дома отвечает поставщик, а не управляющая организация.",
                "ПП РФ от 06.05.2011 № 354"),
            Zone("contractor", "Подрядчик", "В управляющую организацию",
                "Работы выполняет привлечённый подрядчик; обращение подаётся через управляющую организацию как заказчика.",
                "ПП РФ от 15.05.2013 № 416"),
            Zone("municipality", "Орган местного самоуправления", "В администрацию муниципального образования",
                "Вопрос за границей общего имущества дома — обращение в администрацию города или района. "
                + "Подать его можно и через портал «Госуслуги. Решаем вместе».",
                "ЖК РФ"),
            Zone("regional_operator", "Региональный оператор по ТКО", "Региональному оператору по обращению с ТКО",
                "Вывоз отходов организует региональный оператор. Обращение можно направить ему напрямую "
                + "или через управляющую организацию; название и контакты оператора обычно указаны "
                + "в квитанции за обращение с ТКО.",
                "ПП РФ от 12.11.2016 № 1156"),
            Zone("ads", "Аварийно-диспетчерская служба", "В аварийно-диспетчерскую службу",
                "Аварийная ситуация: звоните в АДС, норматив ответа оператора — 5 минут.",
                "ПП РФ от 27.03.2018 № 331"),
            Zone("resident", "Собственник помещения", null,
                "Это внутриквартирная зона: управляющая организация за неё не отвечает. "
                + "При заливе от соседей она составляет акт, но ущерб возмещает виновник.",
                "ЖК РФ, ст. 30"));

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCategoriesAsync(DomovoyDbContext db, CancellationToken ct)
    {
        if (await db.ProblemCategories.AnyAsync(ct)) return;

        var zones = await db.ResponsibilityZones.ToDictionaryAsync(z => z.Code, ct);

        // Первый экран — по каталогу docs/product/08-problem-catalog.md: семь кнопок,
        // авария первой. Подробность — в темах, у которых свои адресаты и сроки.
        var categories = new[]
        {
            Category("emergency", "Авария или опасность", 10, true, null),
            Category("utilities", "Вода, отопление, электричество", 20, false,
                "Что именно случилось?"),
            Category("common_property", "Подъезд и общее имущество", 30, false,
                "Что именно и где?"),
            Category("yard_waste", "Двор и мусор", 40, false, "Что именно?"),
            Category("billing", "Начисления и счётчики", 50, false, "О чём вопрос?"),
            Category("followup", "Заявка или ответ УК", 60, false, "Что с прежней заявкой?"),
            Category("other", "Другое", 90, false, null)
        };

        db.ProblemCategories.AddRange(categories);
        await db.SaveChangesAsync(ct);

        var byCode = categories.ToDictionary(c => c.Code);

        // Правило по умолчанию нужно категориям без тем. У остальных адресата
        // определяет тема, а строка ниже — страховка на случай пустого выбора.
        AddResponsibility(db, byCode["emergency"], zones["ads"]);
        AddResponsibility(db, byCode["utilities"], zones["uk"]);
        AddResponsibility(db, byCode["common_property"], zones["uk"]);
        AddResponsibility(db, byCode["yard_waste"], zones["uk"]);
        AddResponsibility(db, byCode["billing"], zones["uk"]);
        AddResponsibility(db, byCode["followup"], zones["uk"]);
        AddResponsibility(db, byCode["other"], zones["uk"]);

        // Срок категории действует для тем, у которых нет своего. У аварии срока ответа
        // на письменное обращение нет: обращение не формируется, норматив оператора АДС
        // показывается на экране аварии.
        AddDeadline(db, byCode["utilities"], 3, DeadlineUnit.BusinessDays,
            "ПП РФ от 06.05.2011 № 354", "Ответ на жалобу о качестве коммунальной услуги.");
        AddDeadline(db, byCode["common_property"], 10, DeadlineUnit.BusinessDays,
            "ПП РФ от 15.05.2013 № 416", "Обращение к управляющей организации.");
        AddDeadline(db, byCode["yard_waste"], 10, DeadlineUnit.BusinessDays,
            "ПП РФ от 15.05.2013 № 416", "Обращение к управляющей организации.");
        AddDeadline(db, byCode["billing"], 10, DeadlineUnit.BusinessDays,
            "ПП РФ от 15.05.2013 № 416", "Обращение к управляющей организации.");
        AddDeadline(db, byCode["followup"], 10, DeadlineUnit.BusinessDays,
            "ПП РФ от 15.05.2013 № 416", "Повторное обращение к управляющей организации.");
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

    private static ResponsibilityZone Zone(
        string code, string title, string? addressee, string hint, string source) => new()
    {
        Code = code,
        Title = title,
        Addressee = addressee,
        ActionHint = hint,
        Source = DataSource.Official,
        SourceName = source,
        ActualAt = CatalogDate,
        Territory = "РФ"
    };

    private static ProblemCategory Category(
        string code, string title, int sort, bool isEmergency, string? question)
    {
        // Название категории — это подпись кнопки: длинный текст в MAX обрезается.
        if (title.Length > SeedClarifyingOptions.MaxButtonLabel)
        {
            throw new InvalidOperationException(
                $"Название «{title}» длиннее {SeedClarifyingOptions.MaxButtonLabel} символов.");
        }

        return new ProblemCategory
        {
            Code = code,
            Title = title,
            SortOrder = sort,
            IsEmergency = isEmergency,
            ClarifyingQuestion = question,
            Source = DataSource.Official,
            SourceName = "Составлено по ЖК РФ и правилам предоставления коммунальных услуг",
            ActualAt = CatalogDate,
            Territory = "РФ"
        };
    }

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
            ActualAt = CatalogDate
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
            ActualAt = CatalogDate,
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
