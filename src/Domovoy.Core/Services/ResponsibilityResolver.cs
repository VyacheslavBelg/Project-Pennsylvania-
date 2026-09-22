using System.Text;
using Domovoy.Core.Data;
using Domovoy.Core.Domain;
using Domovoy.Core.Reference;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Core.Services;

/// <summary>Разбор проблемы: кто отвечает, в какой срок и что делать.</summary>
public sealed record Resolution(
    ProblemCategory Category,
    ClarifyingOption? Option,
    ResponsibilityZone Zone,
    NormativeDeadline? Deadline,
    Building Building)
{
    public bool IsEmergency => Option?.IsEmergency == true || Category.IsEmergency && Option is null;
}

/// <summary>
/// Определяет зону ответственности и применимый норматив.
///
/// Это и есть ядро продукта: ответ не зависит от того, знаем ли мы название управляющей
/// организации конкретного дома. Зона определяется типом проблемы и уточняющим ответом,
/// срок — категорией, а оба правила федеральные и работают по всей стране.
/// </summary>
public sealed class ResponsibilityResolver(DomovoyDbContext db)
{
    public Task<List<ProblemCategory>> GetCategoriesAsync(CancellationToken ct = default) =>
        db.ProblemCategories.OrderBy(c => c.SortOrder).ToListAsync(ct);

    public Task<ProblemCategory?> GetCategoryAsync(int id, CancellationToken ct = default) =>
        db.ProblemCategories
            .Include(c => c.ClarifyingOptions.OrderBy(o => o.SortOrder))
                .ThenInclude(o => o.ResponsibilityZone)
            .Include(c => c.Deadlines)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<ClarifyingOption?> GetOptionAsync(int id, CancellationToken ct = default) =>
        db.ClarifyingOptions
            .Include(o => o.ResponsibilityZone)
            .Include(o => o.ProblemCategory).ThenInclude(c => c.Deadlines)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Resolution?> ResolveAsync(
        ProblemCategory category, ClarifyingOption? option, Building building,
        CancellationToken ct = default)
    {
        var zone = option?.ResponsibilityZone;

        if (zone is null)
        {
            // У категории нет уточняющего вопроса — берём правило по умолчанию.
            // Способ управления домом учитывается, только если правило его требует.
            var fallback = await db.CategoryResponsibilities
                .Include(r => r.ResponsibilityZone)
                .Where(r => r.ProblemCategoryId == category.Id)
                .Where(r => r.AppliesToManagement == null
                            || r.AppliesToManagement == building.ManagementKind)
                .OrderBy(r => r.AppliesToManagement == null ? 1 : 0)
                .FirstOrDefaultAsync(ct);

            zone = fallback?.ResponsibilityZone;
        }

        if (zone is null)
        {
            return null;
        }

        var deadline = category.Deadlines.Count > 0
            ? category.Deadlines[0]
            : await db.NormativeDeadlines.FirstOrDefaultAsync(d => d.ProblemCategoryId == category.Id, ct);

        return new Resolution(category, option, zone, deadline, building);
    }

    /// <summary>
    /// Текст разбора для пользователя. Каждый вывод сопровождается основанием:
    /// без ссылки на норму ответ в этой теме стоит немного.
    /// </summary>
    public static string Describe(Resolution r, DateTimeOffset? deadlineAt)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Отвечает: {r.Zone.Title}");

        if (r.Option?.Explanation is { Length: > 0 } why)
        {
            sb.AppendLine();
            sb.AppendLine(why);
        }

        if (r.Option?.LegalBasis is { Length: > 0 } basis)
        {
            sb.AppendLine($"Основание: {basis}");
        }

        if (r.Deadline is { } d)
        {
            sb.AppendLine();
            sb.AppendLine($"Нормативный срок: {d.Describe()}");
            sb.AppendLine($"Основание: {d.LegalBasis}");

            if (deadlineAt is { } at)
            {
                sb.AppendLine($"Ответ должен поступить до {at.ToLocalTime():dd.MM.yyyy HH:mm}");
            }
        }

        if (r.Zone.ActionHint is { Length: > 0 } hint)
        {
            sb.AppendLine();
            sb.AppendLine(hint);
        }

        // Контакты — приятное дополнение там, где дом подключён. Основной ответ выше
        // от них не зависит.
        if (r.Building.ManagingOrganization is { } org && r.Zone.Code is "uk")
        {
            sb.AppendLine();
            sb.AppendLine($"Ваша управляющая организация: {org.Name}");
            if (!string.IsNullOrWhiteSpace(org.Phone)) sb.AppendLine($"Телефон: {org.Phone}");
        }

        return sb.ToString().TrimEnd();
    }
}
