using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.Constants;
using SkillsBarter.Models;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleSeeder _roleSeeder;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        UserManager<ApplicationUser> userManager,
        RoleSeeder roleSeeder,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _userManager = userManager;
        _roleSeeder = roleSeeder;
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

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = _userManager.Users.ToList();
            var userList = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.EmailConfirmed,
                    user.CreatedAt,
                    Roles = roles
                });
            }

            return Ok(userList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users");
            return StatusCode(500, new { message = "An error occurred while retrieving users" });
        }
    }

    [HttpPost("{userId:guid}/assign-role")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> AssignRole(Guid userId, [FromBody] AssignRoleRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RoleName))
            {
                return BadRequest(new { message = "Role name is required" });
            }

            var validRoles = new[] { AppRoles.Freemium, AppRoles.Premium, AppRoles.Moderator, AppRoles.Admin };
            if (!validRoles.Contains(request.RoleName))
            {
                return BadRequest(new { message = $"Invalid role. Valid roles are: {string.Join(", ", validRoles)}" });
            }

            var result = await _roleSeeder.AssignRoleToUserAsync(userId, request.RoleName);

            if (result)
            {
                _logger.LogInformation("Admin assigned role {RoleName} to user {UserId}", request.RoleName, userId);
                return Ok(new { message = $"User assigned to role {request.RoleName} successfully" });
            }

            return BadRequest(new { message = "Failed to assign role to user" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role to user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while assigning role" });
        }
    }

    [HttpGet("{id:guid}/details")]
    [Authorize]
    public async Task<IActionResult> GetUserDetails(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Invalid user ID provided to details endpoint");
                return BadRequest(new { message = "Invalid user ID" });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Only users can view their own detailed profile or admins/moderators can view any
            var userRoles = await _userManager.GetRolesAsync(currentUser);
            var isAdminOrModerator = userRoles.Any(role => role == AppRoles.Admin || role == AppRoles.Moderator);
            var isOwner = currentUser.Id.Equals(id);

            if (!isOwner && !isAdminOrModerator)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized access to detailed profile of user {TargetUserId}", currentUser.Id, id);
                return StatusCode(403, new { message = "You are not authorized to view this user's detailed profile" });
            }

            var detailedProfile = await _userService.GetDetailedProfileAsync(id);
            if (detailedProfile == null)
            {
                _logger.LogWarning("User not found with ID: {UserId}", id);
                return NotFound(new { message = "User not found" });
            }

            _logger.LogInformation("User {UserId} accessed detailed profile for user {TargetUserId}", currentUser.Id, id);
            return Ok(new
            {
                success = true,
                profile = detailedProfile
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user details for ID: {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving user details" });
        }
    }
}

public class AssignRoleRequest
{
    public string RoleName { get; set; } = string.Empty;
}
