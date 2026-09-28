namespace Domovoy.Core.Domain;

/// <summary>Пользователь MAX. Персональные данные сверх необходимого не храним.</summary>
public class AppUser
{
    public int Id { get; set; }

    /// <summary>user_id из MAX.</summary>
    public long MaxUserId { get; set; }

    /// <summary>chat_id диалога с ботом — адрес для исходящих сообщений и напоминаний.</summary>
    public long MaxChatId { get; set; }

    /// <summary>Имя из профиля MAX. Для официального обращения не годится.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// ФИО для обращения. Порядок рассмотрения обращений граждан требует указывать
    /// фамилию, имя, отчество и адрес для ответа — без них обращение можно оставить
    /// без рассмотрения.
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>Контакт для ответа: телефон или почта, по желанию пользователя.</summary>
    public string? ContactInfo { get; set; }

    /// <summary>
    /// Сообщение бота, которое сейчас служит экраном. Следующий экран либо заменяет его,
    /// либо, если пользователь написал текст, приходит новым сообщением, а это удаляется.
    /// </summary>
    public string? LastBotMessageId { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public List<UserBuildingLink> Links { get; set; } = [];
    public DialogState? DialogState { get; set; }
}
