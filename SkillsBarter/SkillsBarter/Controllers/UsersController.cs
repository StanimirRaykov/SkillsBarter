using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillsBarter.Constants;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using System.Security.Claims;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IReviewService _reviewService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly RoleSeeder _roleSeeder;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        IReviewService reviewService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        RoleSeeder roleSeeder,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _reviewService = reviewService;
        _userManager = userManager;
        _dbContext = dbContext;
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

    [HttpGet("{id:guid}/reviews")]
    public async Task<IActionResult> GetUserReviews(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Invalid user ID provided to reviews endpoint");
                return BadRequest(new { message = "Invalid user ID" });
            }

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                _logger.LogWarning("User not found for reviews endpoint: {UserId}", id);
                return NotFound(new { message = "User not found" });
            }

            var reviewsWithSummary = await _reviewService.GetUserReviewsWithSummaryAsync(id, page, pageSize);

            _logger.LogInformation("Retrieved reviews for user {UserId} - Total: {Total}, Page: {Page}",
                id, reviewsWithSummary.Summary.TotalReviews, page);

            return Ok(reviewsWithSummary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for user: {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving reviews" });
        }
    }

    [HttpPost("{id:guid}/skills")]
    [Authorize]
    public async Task<IActionResult> AddUserSkill(Guid id, [FromBody] AddSkillRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            {
                return Unauthorized(new { message = "Invalid user authentication" });
            }

            if (currentUserId != id)
            {
                return StatusCode(403, new { message = "You can only add skills to your own profile" });
            }

            var skillExists = await _dbContext.Skills.AnyAsync(s => s.Id == request.SkillId);
            if (!skillExists)
            {
                return BadRequest(new { message = "Skill not found" });
            }

            var userSkillExists = await _dbContext.UserSkills
                .AnyAsync(us => us.UserId == id && us.SkillId == request.SkillId);

            if (userSkillExists)
            {
                return BadRequest(new { message = "User already has this skill" });
            }

            var userSkill = new UserSkill
            {
                UserId = id,
                SkillId = request.SkillId,
                AddedAt = DateTime.UtcNow
            };

            await _dbContext.UserSkills.AddAsync(userSkill);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} added skill {SkillId}", id, request.SkillId);

            return Ok(new { message = "Skill added successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding skill for user: {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while adding the skill" });
        }
    }

    [HttpDelete("{id:guid}/skills/{skillId:int}")]
    [Authorize]
    public async Task<IActionResult> RemoveUserSkill(Guid id, int skillId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            {
                return Unauthorized(new { message = "Invalid user authentication" });
            }

            if (currentUserId != id)
            {
                return StatusCode(403, new { message = "You can only remove skills from your own profile" });
            }

            var userSkill = await _dbContext.UserSkills
                .FirstOrDefaultAsync(us => us.UserId == id && us.SkillId == skillId);

            if (userSkill == null)
            {
                return NotFound(new { message = "User skill not found" });
            }

            _dbContext.UserSkills.Remove(userSkill);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} removed skill {SkillId}", id, skillId);

            return Ok(new { message = "Skill removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing skill for user: {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while removing the skill" });
        }
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetMyProfile()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user authentication" });
            }

            var profile = await _userService.GetDetailedProfileAsync(userId);
            if (profile == null)
            {
                _logger.LogWarning("Profile not found for authenticated user {UserId}", userId);
                return NotFound(new { message = "Profile not found" });
            }

            return Ok(new { success = true, profile });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile for authenticated user");
            return StatusCode(500, new { message = "An error occurred while retrieving your profile" });
        }
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid input", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user authentication" });
            }

            var updatedProfile = await _userService.UpdateProfileAsync(userId, request);
            if (updatedProfile == null)
            {
                _logger.LogWarning("Failed to update profile for user {UserId}", userId);
                return NotFound(new { message = "Profile not found" });
            }

            _logger.LogInformation("User {UserId} updated their profile", userId);
            return Ok(new
            {
                success = true,
                message = "Profile updated successfully",
                profile = updatedProfile
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while updating profile");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for authenticated user");
            return StatusCode(500, new { message = "An error occurred while updating your profile" });
        }
    }
}
