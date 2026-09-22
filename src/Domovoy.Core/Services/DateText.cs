namespace Domovoy.Core.Services;

/// <summary>
/// Даты словами.
///
/// MAX сам превращает шаблон «дд.мм.гггг чч:мм» в ссылку и подсвечивает его,
/// причём захватывает не все символы — последняя цифра остаётся неокрашенной.
/// Запись месяцем прописью этот автоматический разбор не запускает и читается лучше.
/// </summary>
public static class DateText
{
    private static readonly string[] Months =
    [
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря"
    ];

    /// <summary>«25 сентября 2026».</summary>
    public static string Date(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        return $"{local.Day} {Months[local.Month - 1]} {local.Year}";
    }

    /// <summary>«25 сентября 2026, 09:43».</summary>
    public static string DateTime(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        return $"{Date(value)}, {local:HH}:{local:mm}";
    }

    /// <summary>«25 сентября 2026 г.» — для текста обращения.</summary>
    public static string DocumentDate(DateTimeOffset value) => $"{Date(value)} г.";
}
