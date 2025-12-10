using SkillsBarter.DTOs.Notifications;

namespace SkillsBarter.Services;

public interface INotificationService
{
    Task<NotificationsListResponseDto> GetNotificationsAsync(Guid userId, bool unreadOnly, int skip, int take);
    Task<int?> MarkNotificationsAsReadAsync(Guid userId, MarkReadRequestDto request);
    Task CreateAsync(Guid userId, string type, string title, string message);
}

