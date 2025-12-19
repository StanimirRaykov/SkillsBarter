using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillsBarter.Constants;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Moderator}")]
public class AdminController : ControllerBase
{
    private readonly IDisputeService _disputeService;
    private readonly IAgreementService _agreementService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleSeeder _roleSeeder;
    private readonly ApplicationDbContext _dbContext;

    public AdminController(
        IDisputeService disputeService,
        IAgreementService agreementService,
        UserManager<ApplicationUser> userManager,
        RoleSeeder roleSeeder,
        ApplicationDbContext dbContext)
    {
        _disputeService = disputeService;
        _agreementService = agreementService;
        _userManager = userManager;
        _roleSeeder = roleSeeder;
        _dbContext = dbContext;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _userManager.Users.AsNoTracking();

        var totalUsers = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userDtos = new List<AdminUserDto>();

        var userIds = users.Select(u => u.Id).ToList();
        var userRolesMap = await _dbContext.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name })
            .ToListAsync();

        foreach (var user in users)
        {
            var roles = userRolesMap
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.RoleName ?? string.Empty)
                .ToList();

            userDtos.Add(new AdminUserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? string.Empty,
                Roles = roles,
                IsBanned = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                LockoutEnd = user.LockoutEnd,
                CreatedAt = user.CreatedAt
            });
        }

        return Ok(new PaginatedResponse<AdminUserDto>
        {
            Items = userDtos,
            Page = page,
            PageSize = pageSize,
            Total = totalUsers
        });
    }

    [HttpPut("users/{id:guid}/ban")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] AdminUpdateUserStatusRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return NotFound(new { message = "User not found" });

        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == user.Id.ToString())
            return BadRequest(new { message = "You cannot ban yourself." });

        // Prevent banning other admins to avoid locking out all admins
        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
            return BadRequest(new { message = "Cannot ban other administrators." });

        if (request.IsBanned)
        {
            var permanentBanDate = DateTimeOffset.UtcNow.AddYears(100);
            await _userManager.SetLockoutEndDateAsync(user, permanentBanDate);
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
        }

        return Ok(new { message = $"User {(request.IsBanned ? "banned" : "unbanned")} successfully." });
    }

    [HttpPut("users/{id:guid}/role")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] AdminUpdateUserRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Role))
            return BadRequest(new { message = "Role is required" });

        var validRoles = new[] { AppRoles.Freemium, AppRoles.Premium, AppRoles.Moderator, AppRoles.Admin };
        if (!validRoles.Contains(request.Role))
            return BadRequest(new { message = $"Invalid role. Valid roles are: {string.Join(", ", validRoles)}" });

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return NotFound(new { message = "User not found" });

        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == user.Id.ToString())
            return BadRequest(new { message = "You cannot change your own role." });

        // Prevent changing other admins' roles
        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
            return BadRequest(new { message = "Cannot change the role of other administrators." });

        var result = await _roleSeeder.AssignRoleToUserAsync(id, request.Role);
        if (!result)
            return BadRequest(new { message = "Failed to update user role." });

        bool shouldBeModerator = request.Role == AppRoles.Moderator || request.Role == AppRoles.Admin;
        if (user.IsModerator != shouldBeModerator)
        {
            user.IsModerator = shouldBeModerator;
            await _userManager.UpdateAsync(user);
        }

        return Ok(new { message = $"User role updated to {request.Role} successfully." });
    }

    [HttpGet("agreements")]
    public async Task<IActionResult> GetAllAgreements(
        [FromQuery] int? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        Models.AgreementStatus? agreementStatus = status.HasValue
            ? (Models.AgreementStatus)status.Value
            : null;

        var result = await _agreementService.GetAllAgreementsAsync(agreementStatus, page, pageSize);
        return Ok(result);
    }

    [HttpGet("disputes")]
    public async Task<IActionResult> GetAllActiveDisputes()
    {
        var disputes = await _disputeService.GetAllActiveDisputesAsync();
        return Ok(disputes);
    }

    [HttpGet("disputes/{id:guid}")]
    public async Task<IActionResult> GetDispute(Guid id)
    {
        var dispute = await _disputeService.GetDisputeForAdminAsync(id);
        if (dispute == null)
            return NotFound(new { message = "Dispute not found" });

        return Ok(dispute);
    }

    [HttpPut("disputes/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveDispute(
        Guid id,
        [FromBody] AdminResolveDisputeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request", errors = ModelState });

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { message = "User not authenticated" });

        var result = await _disputeService.AdminResolveDisputeAsync(id, request, user.Id);
        if (result == null)
            return BadRequest(new { message = "Failed to resolve dispute. It may not exist or is already resolved." });

        return Ok(new { message = "Dispute resolved successfully", dispute = result });
    }
}
