using Domovoy.Core.Max;
using Microsoft.Extensions.Options;

namespace Domovoy.Api.Bot;

/// <summary>
/// Кнопки, открывающие формы мини-приложения.
///
/// Через формы идёт весь свободный ввод: адрес, описание проблемы, ФИО и квартира.
/// Причина одна — сообщения пользователя бот удалить не может, платформа отвечает
/// на это 403, и каждая набранная строка навсегда оставалась в переписке.
///
/// Формы включаются, только когда бэкенд доступен по публичному HTTPS: иначе кнопка
/// открыла бы страницу, которая не может ни искать, ни сохранять. Имя переменной
/// Max__BindForm осталось от первой формы и теперь шире своего названия —
/// переименование отложено, чтобы не трогать прод перед сдачей.
/// </summary>
public sealed class WebForms(IOptions<MaxBotOptions> options)
{
    private readonly MaxBotOptions _options = options.Value;

    public bool Enabled => _options.HasMiniApp && _options.BindForm;

    public object Button(string text, string screen) =>
        MaxButton.Link(text, _options.MiniAppLink(screen));
}

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

    /// <summary>Длина подписи кнопки, дальше MAX обрезает сам и без многоточия.</summary>
    public const int MaxButtonLabel = 34;

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
