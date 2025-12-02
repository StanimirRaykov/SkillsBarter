using SkillsBarter.Models;
using SkillsBarter.Data;
using Microsoft.EntityFrameworkCore;

public interface INotificationService
{
    Task<List<Notification>> GetUserNotificationsAsync(Guid userId, bool onlyUnread);
    Task MarkAsReadAsync(Guid userId, List<Guid> notificationIds);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _dbContext;

    public NotificationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(Guid userId, bool onlyUnread)
    {
        var query = _dbContext.Notifications.Where(n => n.UserId == userId);
        if (onlyUnread)
            query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid userId, List<Guid> notificationIds)
    {
        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && notificationIds.Contains(n.Id))
            .ToListAsync();

        foreach (var n in notifications)
            n.IsRead = true;

        await _dbContext.SaveChangesAsync();
    }
}
