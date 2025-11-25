using Microsoft.AspNetCore.Mvc;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserProfile(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Invalid user ID provided");
                return BadRequest(new { message = "Invalid user ID" });
            }

            var profile = await _userService.GetPublicProfileAsync(id);

            if (profile == null)
            {
                _logger.LogWarning("User profile not found for ID: {UserId}", id);
                return NotFound(new { message = "User not found" });
            }

            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile for ID: {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the user profile" });
        }
    }
}
