namespace SkillsBarter.DTOs.Notifications;

public class NotificationsListResponseDto
{
    public List<NotificationItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
}

