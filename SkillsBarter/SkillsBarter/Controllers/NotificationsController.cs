using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.DTOs.Notifications;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationsListResponseDto>> GetNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        Guid? userIdForLog = null;

        try
        {
            if (!TryGetUserId(out var userId))
            {
                _logger.LogWarning("Unable to determine user ID for notifications fetch");
                return Unauthorized(new { message = "User context missing" });
            }

            userIdForLog = userId;
            var response = await _notificationService.GetNotificationsAsync(userId, unreadOnly, skip, take);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications for user {UserId}", userIdForLog);
            return StatusCode(500, new { message = "An error occurred while retrieving notifications" });
        }
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkReadRequestDto? request)
    {
        Guid? userIdForLog = null;

        try
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request body is required" });
            }

            if (!TryGetUserId(out var userId))
            {
                _logger.LogWarning("Unable to determine user ID for mark-read");
                return Unauthorized(new { message = "User context missing" });
            }

            userIdForLog = userId;
            var updated = await _notificationService.MarkNotificationsAsReadAsync(userId, request);

            if (!updated.HasValue)
            {
                return BadRequest(new { message = "Provide notification ids or set markAll to true" });
            }

            return Ok(new { success = true, updated = updated.Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notifications as read for user {UserId}", userIdForLog);
            return StatusCode(500, new { message = "An error occurred while updating notifications" });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var parsed))
        {
            userId = parsed;
            return true;
        }

        userId = Guid.Empty;
        return false;
    }
}

