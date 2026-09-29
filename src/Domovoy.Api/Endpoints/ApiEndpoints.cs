using Domovoy.Api.Auth;
using Domovoy.Api.Bot;
using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Api.Endpoints;

/// <summary>Выбор дома в форме: свой дом по идентификатору либо дом из реестра по ФИАС.</summary>
public sealed record BindBuildingRequest(int? BuildingId, string? FiasId, string? Query);

/// <summary>
/// REST для мини-приложения.
///
/// Внутренний интерфейс между нашим бэкендом и нашим же фронтендом. Это не «решение
/// с собственным API» в смысле формата сдачи: такой API заявляется отдельно и требует
/// OpenAPI и DATA-API.yaml, а баллов сам по себе не даёт.
///
/// Справочные эндпоинты открыты. Действия от имени пользователя принимаются только
/// с подписанным платформой initData: идентификатору в теле запроса верить нельзя.
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

            // Раскладка по населённым пунктам та же, что в боте: выглядят одинаково.
            return Results.Ok(new
            {
                total = found.Count,
                limitReached = found.Count >= AddressLookupService.MaxResults,
                fromRegistryOnly = found.Count > 0 && found.All(c => !c.KnownHouse),
                groups = AddressGrouping.Group(found).Select(g => new
                {
                    header = g.Header,
                    items = g.Items.Select(i =>
                    {
                        var c = found[i.Index];
                        return new
                        {
                            label = i.Label,
                            display = c.Display,
                            buildingId = c.BuildingId,
                            fiasId = c.Suggested?.FiasId,
                            managingOrganization = c.ManagingOrganizationName
                        };
                    })
                })
            });
        });

        // Привязка дома из формы мини-приложения. Ввод в форме не оставляет сообщений
        // в переписке, а результат показывается в чате: карточка дома сменяет экран ввода.
        app.MapPost("/api/me/building", async (
            BindBuildingRequest body,
            HttpRequest http,
            MaxInitDataValidator auth,
            DomovoyDbContext db,
            AddressLookupService lookup,
            BindingScenario binding,
            BotScreen screen,
            CancellationToken ct) =>
        {
            if (auth.Validate(http.Headers[MaxInitDataValidator.Header]) is not { } identity)
            {
                return Results.Problem("Откройте форму из бота в MAX.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await db.Users
                .Include(u => u.DialogState)
                .FirstOrDefaultAsync(u => u.MaxUserId == identity.UserId, ct);

            // Результат показывается в чате с ботом, а чат известен только после того,
            // как пользователь хотя бы раз ему написал.
            if (user is null)
            {
                return Results.Problem("Сначала откройте бота в MAX.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var buildingId = body.BuildingId;

            // Дом из реестра ищется заново по тому же запросу: адресу, присланному
            // клиентом, не доверяем — берём только его идентификатор в ФИАС.
            if (buildingId is null && body.FiasId is { Length: > 0 } fias && body.Query is { Length: > 0 } query)
            {
                var found = await lookup.FindAsync(query, ct);
                var match = found.FirstOrDefault(c =>
                    string.Equals(c.Suggested?.FiasId, fias, StringComparison.OrdinalIgnoreCase));

                if (match?.Suggested is { } suggested)
                {
                    buildingId = (await lookup.EnsureBuildingAsync(suggested, ct)).Id;
                }
            }

            if (buildingId is null)
            {
                return Results.Problem("Адрес не найден, попробуйте ещё раз.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            screen.Begin(user, null, null);
            var building = await binding.LinkBuildingAsync(user, buildingId.Value, ct);

            return building is null
                ? Results.Problem("Этот дом больше недоступен.", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(new { address = building.Address.ToFullString() });
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
                // Срок категории, а не первой попавшейся темы: у тем бывают свои нормы.
                deadline = ResponsibilityResolver.PickDeadline(c.Deadlines, null) is { } d
                    ? new { text = d.Describe(), d.LegalBasis }
                    : null,
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
            full = b.Address.ToFullString()
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
