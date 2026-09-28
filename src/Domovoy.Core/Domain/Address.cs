namespace Domovoy.Core.Domain;

/// <summary>
/// Нормализованный адрес дома. FiasId заполняется, когда адрес сверен с государственным
/// адресным реестром; в MVP данные подготовленные, поэтому поле обычно пустое.
/// </summary>
public class Address
{
    public int Id { get; set; }

    public required string Region { get; set; }
    public required string City { get; set; }
    public required string Street { get; set; }
    public required string House { get; set; }

    /// <summary>Корпус или строение в виде «к 1». Отличает 12к1 от 12к2.</summary>
    public string? Block { get; set; }

    public string? FiasId { get; set; }

    /// <summary>Нормализованная строка в нижнем регистре — по ней идёт поиск по вводу пользователя.</summary>
    public required string SearchText { get; set; }

    public Building? Building { get; set; }

    public override string ToString() => Block is { Length: > 0 } block
        ? $"{City}, {Street}, д. {House}, {block}"
        : $"{City}, {Street}, д. {House}";

    /// <summary>
    /// Адрес с регионом — для карточки дома и текста обращения. Без региона «рп Новоспасское»
    /// или «пгт Октябрьский» неоднозначны: одноимённых посёлков в стране десятки, а адрес
    /// уходит в документ. У городов федерального значения реестр отдаёт регион и город
    /// одинаковыми, и тогда регион не повторяется.
    /// </summary>
    public string ToFullString() =>
        Region is { Length: > 0 } region && !string.Equals(region, City, StringComparison.OrdinalIgnoreCase)
            ? $"{region}, {this}"
            : ToString();
}
