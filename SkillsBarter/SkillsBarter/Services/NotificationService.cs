using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs.Notifications;
using SkillsBarter.Hubs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(
        ApplicationDbContext dbContext,
        ILogger<NotificationService> logger,
        IHubContext<NotificationHub> hubContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task CreateAsync(Guid userId, string type, string title, string message)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();

        try
        {
            var notificationDto = new NotificationDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                CreatedAt = notification.CreatedAt
            };

            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("ReceiveNotification", notificationDto);

            _logger.LogInformation("Real-time notification sent to user {UserId} via SignalR", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send real-time notification to user {UserId}", userId);
        }
    }

    public async Task<NotificationsListResponseDto> GetNotificationsAsync(Guid userId, bool unreadOnly, int skip, int take)
    {
        if (skip < 0)
        {
            skip = 0;
        }

        if (take <= 0)
        {
            take = 20;
        }

        if (take > 100)
        {
            take = 100;
        }

        try
        {
            var baseQuery = _dbContext.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId);

            var filteredQuery = unreadOnly ? baseQuery.Where(n => !n.IsRead) : baseQuery;

            var totalCount = await filteredQuery.CountAsync();
            var unreadCount = await baseQuery.Where(n => !n.IsRead).CountAsync();
            var items = await filteredQuery
                .OrderByDescending(n => n.CreatedAt)
                .Skip(skip)
                .Take(take)
                .Select(n => new NotificationItemDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt,
                    ReadAt = n.ReadAt
                })
                .ToListAsync();

            return new NotificationsListResponseDto
            {
                Items = items,
                TotalCount = totalCount,
                UnreadCount = unreadCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int?> MarkNotificationsAsReadAsync(Guid userId, MarkReadRequestDto request)
    {
        try
        {
            var userNotifications = _dbContext.Notifications
                .Where(n => n.UserId == userId && !n.IsRead);
            var now = DateTimeOffset.UtcNow;

            if (request.MarkAll)
            {
                return await userNotifications.ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.IsRead, _ => true)
                    .SetProperty(n => n.ReadAt, _ => now));
            }

            if (request.Ids == null || request.Ids.Count == 0)
            {
                _logger.LogWarning("Mark notifications failed: no ids provided for user {UserId}", userId);
                return null;
            }

            var ids = request.Ids
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                _logger.LogWarning("Mark notifications failed: invalid ids provided for user {UserId}", userId);
                return null;
            }

            var targeted = userNotifications.Where(n => ids.Contains(n.Id));

            return await targeted.ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, _ => true)
                .SetProperty(n => n.ReadAt, _ => now));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notifications as read for user {UserId}", userId);
            throw;
        }
    }
}

