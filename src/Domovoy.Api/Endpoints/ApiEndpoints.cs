using Domovoy.Api.Auth;
using Domovoy.Api.Bot;
using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Api.Endpoints;

/// <summary>Выбор дома в форме: свой дом по идентификатору либо дом из реестра по ФИАС.</summary>
public sealed record BindBuildingRequest(int? BuildingId, string? FiasId, string? Query);

/// <summary>Описание проблемы своими словами из формы.</summary>
public sealed record DescriptionRequest(string? Text);

/// <summary>Данные жителя для обращения. Пустое поле означает «не менять».</summary>
public sealed record ProfileRequest(string? FullName, string? Apartment);

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

        // Действия из формы мини-приложения. Ввод в форме не оставляет сообщений
        // в переписке, а результат показывается в чате: экран бота сменяется следующим.
        //
        // Пользователь определяется только по подписанному платформой initData.
        // Идентификатору в теле запроса верить нельзя: форма открыта на стороннем домене.
        async Task<(AppUser User, IResult? Error)> IdentifyAsync(
            HttpRequest http, MaxInitDataValidator auth, DomovoyDbContext db,
            BotScreen screen, CancellationToken ct)
        {
            if (auth.Validate(http.Headers[MaxInitDataValidator.Header]) is not { } identity)
            {
                return (null!, Results.Problem("Откройте форму из бота в MAX.",
                    statusCode: StatusCodes.Status401Unauthorized));
            }

            var user = await db.Users
                .Include(u => u.DialogState)
                .FirstOrDefaultAsync(u => u.MaxUserId == identity.UserId, ct);

            // Результат показывается в чате с ботом, а чат известен только после того,
            // как пользователь хотя бы раз ему написал.
            if (user is null)
            {
                return (null!, Results.Problem("Сначала откройте бота в MAX.",
                    statusCode: StatusCodes.Status404NotFound));
            }

            screen.Begin(user, null, null);
            return (user, null);
        }

        // Описание проблемы: раньше его набирали сообщением в чат, и оно оставалось
        // в переписке — удалять сообщения пользователя платформа боту не даёт.
        app.MapPost("/api/me/request/description", async (
            DescriptionRequest body,
            HttpRequest http,
            MaxInitDataValidator auth,
            DomovoyDbContext db,
            ProblemScenario problem,
            BotScreen screen,
            CancellationToken ct) =>
        {
            var (user, error) = await IdentifyAsync(http, auth, db, screen, ct);
            if (error is not null) return error;

            var text = body.Text?.Trim();
            await problem.HandleDescriptionAsync(user, text is { Length: > 0 } ? text : null, ct);

            return Results.Ok(new { saved = true });
        });

        // Данные жителя: ФИО и квартира нужны, чтобы обращение не оставили
        // без рассмотрения, и в чате им тоже не место.
        app.MapPost("/api/me/profile", async (
            ProfileRequest body,
            HttpRequest http,
            MaxInitDataValidator auth,
            DomovoyDbContext db,
            ProfileScenario profile,
            BotScreen screen,
            CancellationToken ct) =>
        {
            var (user, error) = await IdentifyAsync(http, auth, db, screen, ct);
            if (error is not null) return error;

            await profile.SaveProfileAsync(user, body.FullName, body.Apartment, ct);

            return Results.Ok(new { saved = true });
        });

        // Обращения жителя для карточки в мини-приложении.
        //
        // Срок отдаётся моментом времени, а не строкой «осталось 9 дн.»: в чате такая
        // строка устаревает сразу после отправки, а приложение считает остаток само
        // и обновляет его на глазах. Ради этого мини-приложение и бралось в объём.
        app.MapGet("/api/me/requests", async (
            HttpRequest http,
            MaxInitDataValidator auth,
            DomovoyDbContext db,
            CancellationToken ct) =>
        {
            if (auth.Validate(http.Headers[MaxInitDataValidator.Header]) is not { } identity)
            {
                return Results.Problem("Откройте приложение из бота в MAX.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.MaxUserId == identity.UserId, ct);
            if (user is null)
            {
                return Results.Problem("Сначала откройте бота в MAX.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var requests = await db.Requests
                .Include(r => r.ProblemCategory)
                .Include(r => r.ResponsibilityZone)
                .Include(r => r.Building).ThenInclude(b => b.Address)
                .Where(r => r.AppUserId == user.Id && r.Status != RequestStatus.Draft)
                .OrderByDescending(r => r.SubmittedAt ?? r.CreatedAt)
                .Take(20)
                .ToListAsync(ct);

            return Results.Ok(requests.Select(r => new
            {
                r.Id,
                r.Number,
                subject = r.Subject ?? r.ProblemCategory.Title,
                status = r.Status.ToString(),
                zone = r.ResponsibilityZone?.Title,
                address = r.Building.Address.ToFullString(),
                submittedAt = r.SubmittedAt ?? r.CreatedAt,
                r.DeadlineAt,
                r.DeadlineDescription,
                r.DeadlineLegalBasis,
                r.GeneratedText
            }));
        });

        // Текущие данные жителя — чтобы форма открылась уже заполненной.
        app.MapGet("/api/me/profile", async (
            HttpRequest http,
            MaxInitDataValidator auth,
            DomovoyDbContext db,
            CancellationToken ct) =>
        {
            if (auth.Validate(http.Headers[MaxInitDataValidator.Header]) is not { } identity)
            {
                return Results.Problem("Откройте форму из бота в MAX.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.MaxUserId == identity.UserId, ct);
            if (user is null)
            {
                return Results.Problem("Сначала откройте бота в MAX.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var link = await db.UserBuildingLinks
                .Include(l => l.Building).ThenInclude(b => b.Address)
                .FirstOrDefaultAsync(l => l.AppUserId == user.Id, ct);

            return Results.Ok(new
            {
                fullName = user.FullName,
                apartment = link?.Apartment,
                address = link?.Building.Address.ToFullString()
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
