using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using SkillsBarter.Models;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(INotificationService notificationService, UserManager<ApplicationUser> userManager)
    {
        _notificationService = notificationService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool onlyUnread = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var notifications = await _notificationService.GetUserNotificationsAsync(user.Id, onlyUnread);
        return Ok(notifications);
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromBody] List<Guid> notificationIds)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        await _notificationService.MarkAsReadAsync(user.Id, notificationIds);
        return NoContent();
    }
}
