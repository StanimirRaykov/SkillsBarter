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
    [Authorize(Roles = $"{AppRoles.Moderator},{AppRoles.Admin}")]
    public async Task<IActionResult> GetUserDetails(Guid id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                user.Id,
                user.Name,
                user.Email,
                user.EmailConfirmed,
                user.PhoneNumber,
                user.Description,
                user.VerificationLevel,
                user.ReputationScore,
                user.CreatedAt,
                user.UpdatedAt,
                Roles = roles
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
