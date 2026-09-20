using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Api.Endpoints;

/// <summary>
/// REST для мини-приложения.
///
/// Внутренний интерфейс между нашим бэкендом и нашим же фронтендом. Это не «решение
/// с собственным API» в смысле формата сдачи: такой API заявляется отдельно и требует
/// OpenAPI и DATA-API.yaml, а баллов сам по себе не даёт.
///
/// Проверки подписи initData здесь пока нет — она появится в Фазе 4 вместе
/// с полноценной карточкой обращения. До тех пор идентификатор пользователя
/// на веру не принимается: эндпоинты отдают только справочные данные.
/// </summary>
public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // Тот же поиск, что и у бота: сначала своя база, затем государственный
        // адресный реестр. Дом из реестра не сохраняется до выбора пользователем.
        app.MapGet("/api/buildings/search", async (
            string query, AddressLookupService lookup, CancellationToken ct) =>
        {
            var found = await lookup.FindAsync(query, ct);

            return Results.Ok(found.Select(c => new
            {
                buildingId = c.BuildingId,
                display = c.Display,
                knownHouse = c.KnownHouse,
                managingOrganization = c.ManagingOrganizationName,
                fiasId = c.Suggested?.FiasId,
                source = c.KnownHouse
                    ? "Данные о доме загружены в систему"
                    : "Адрес распознан по государственному адресному реестру (ФИАС); "
                      + "сведений об управляющей организации нет"
            }));
        });

        app.MapGet("/api/buildings/{id:int}", async (
            int id, BuildingSearchService search, CancellationToken ct) =>
        {
            var building = await search.GetAsync(id, ct);
            return building is null ? Results.NotFound() : Results.Ok(ToDto(building));
        });

        app.MapGet("/api/reference/categories", async (DomovoyDbContext db, CancellationToken ct) =>
        {
            var categories = await db.ProblemCategories
                .Include(c => c.Deadlines)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(ct);

            return Results.Ok(categories.Select(c => new
            {
                c.Code,
                c.Title,
                c.IsEmergency,
                deadline = c.Deadlines
                    .Select(d => new { text = d.Describe(), d.LegalBasis })
                    .FirstOrDefault(),
                source = new { name = c.SourceName, actualAt = c.ActualAt }
            }));
        });
    }

    private static object ToDto(Building b) => new
    {
        b.Id,
        address = new
        {
            b.Address.Region,
            b.Address.City,
            b.Address.Street,
            b.Address.House,
            full = b.Address.ToString()
        },
        management = new
        {
            kind = b.ManagementKind.ToString(),
            name = b.ManagingOrganization?.Name,
            phone = b.ManagingOrganization?.Phone,
            emergencyPhone = b.ManagingOrganization?.EmergencyPhone
        },
        b.BuildYear,
        b.Floors,
        b.Entrances,
        // Происхождение данных отдаётся вместе с ними: интерфейс обязан показать,
        // что это демонстрационный набор, а не выгрузка ГИС ЖКХ.
        source = new
        {
            kind = b.Source.ToString(),
            name = b.SourceName,
            actualAt = b.ActualAt,
            isTestData = b.Source == DataSource.TestData
        }
    };
}
