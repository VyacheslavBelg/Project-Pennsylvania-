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

    /// <summary>Улица и дом: «ул. Баумана, д. 15, к 1».</summary>
    public string StreetLine => Block is { Length: > 0 } block
        ? $"{Street}, д. {House}, {block}"
        : $"{Street}, д. {House}";

    /// <summary>
    /// Населённый пункт с регионом. У городов федерального значения реестр отдаёт регион
    /// и город одинаковыми («г Москва»), и тогда регион не повторяется.
    /// </summary>
    public string Locality =>
        Region is { Length: > 0 } region && !string.Equals(region, City, StringComparison.OrdinalIgnoreCase)
            ? $"{region}, {City}"
            : City;

    public override string ToString() => $"{City}, {StreetLine}";

    /// <summary>
    /// Адрес с регионом — для карточки дома и текста обращения. Без региона «рп Новоспасское»
    /// или «пгт Октябрьский» неоднозначны: одноимённых посёлков в стране десятки, а адрес
    /// уходит в документ.
    /// </summary>
    public string ToFullString() => $"{Locality}, {StreetLine}";
}
