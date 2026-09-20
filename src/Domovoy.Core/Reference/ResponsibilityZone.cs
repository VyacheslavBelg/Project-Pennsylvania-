using Domovoy.Core.Domain;

namespace Domovoy.Core.Reference;

/// <summary>
/// Зона ответственности: кто устраняет проблему — управляющая организация, ресурсоснабжающая
/// организация, подрядчик или муниципальная служба. Неизвестность этого и есть проблема,
/// ради которой существует продукт.
/// </summary>
public class ResponsibilityZone
{
    public int Id { get; set; }

    public required string Code { get; set; }
    public required string Title { get; set; }

    /// <summary>Что пользователю делать: куда обращаться и в каком порядке.</summary>
    public string? ActionHint { get; set; }

    public DataSource Source { get; set; }
    public string? SourceName { get; set; }
    public DateOnly? ActualAt { get; set; }
    public string? Territory { get; set; }

    public List<CategoryResponsibility> Responsibilities { get; set; } = [];
}
