using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace SkillsBarter.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.Value}");
            _logger.LogInformation("User {UserId} connected to NotificationHub with connection {ConnectionId}",
                userId.Value, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("Anonymous user attempted to connect to NotificationHub");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            _logger.LogInformation("User {UserId} disconnected from NotificationHub with connection {ConnectionId}",
                userId.Value, Context.ConnectionId);
        }

        if (exception != null)
        {
            _logger.LogError(exception, "User disconnected with error");
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}
