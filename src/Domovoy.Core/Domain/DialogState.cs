namespace Domovoy.Core.Domain;

/// <summary>
/// Состояние диалога хранится в базе, а не в памяти процесса: критерий стабильности требует,
/// чтобы после ошибки или перезапуска пользователь мог продолжить без потери контекста.
/// </summary>
public class DialogState
{
    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    /// <summary>Текущий шаг сценария.</summary>
    public required string Step { get; set; }

    /// <summary>Введённые пользователем данные текущего шага в виде JSON.</summary>
    public string? PayloadJson { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
