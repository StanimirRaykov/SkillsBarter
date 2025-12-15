using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.Constants;
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
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(
        IDisputeService disputeService,
        UserManager<ApplicationUser> userManager)
    {
        _disputeService = disputeService;
        _userManager = userManager;
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
