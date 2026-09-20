namespace Domovoy.Core.Domain;

/// <summary>Пользователь MAX. Персональные данные сверх необходимого не храним.</summary>
public class AppUser
{
    public int Id { get; set; }

    /// <summary>user_id из MAX.</summary>
    public long MaxUserId { get; set; }

    /// <summary>chat_id диалога с ботом — адрес для исходящих сообщений и напоминаний.</summary>
    public long MaxChatId { get; set; }

    public string? DisplayName { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public List<UserBuildingLink> Links { get; set; } = [];
    public DialogState? DialogState { get; set; }
}
