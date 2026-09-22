using Domovoy.Core.Max;

namespace Domovoy.Api.Bot;

/// <summary>
/// Единые правила навигации.
///
/// Критерий требует, чтобы в сценарии не было тупиков. Добавлять возврат вручную на
/// каждом экране ненадёжно — один забытый и пользователь застревает, а заметно это
/// только при живой проверке. Поэтому кнопка возврата приклеивается ко всем экранам,
/// кроме самого главного меню.
/// </summary>
public static class BotUi
{
    public const string MenuCallback = "nav:menu";

    private const string MenuLabel = "‹ Главное меню";

    /// <summary>Добавляет возврат в меню последней строкой.</summary>
    public static List<List<object>> WithMenu(List<List<object>>? rows = null)
    {
        // Новый список, а не изменение переданного: вызывающий может держать свой набор
        // кнопок и переиспользовать его.
        List<List<object>> result = rows is null ? [] : [.. rows];
        result.Add(MenuRow);
        return result;
    }

    /// <summary>Готовая строка для мест, где кнопки собираются вручную.</summary>
    public static List<object> MenuRow => [MaxButton.Callback(MenuLabel, MenuCallback)];
}
