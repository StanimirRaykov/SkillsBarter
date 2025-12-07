using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly IDisputeService _disputeService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DisputesController(
        IDisputeService disputeService,
        UserManager<ApplicationUser> userManager
    )
    {
        _disputeService = disputeService;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<IActionResult> OpenDispute([FromBody] OpenDisputeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request", errors = ModelState });

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { message = "User not authenticated" });

        var result = await _disputeService.OpenDisputeAsync(request, user.Id);
        if (result == null)
            return BadRequest(
                new
                {
                    message = "Failed to open dispute. Ensure the agreement exists, is in progress, and no active dispute exists.",
                }
            );

        return CreatedAtAction(nameof(GetDispute), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDispute(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { message = "User not authenticated" });

        var result = await _disputeService.GetDisputeByIdAsync(id, user.Id);
        if (result == null)
            return NotFound(new { message = "Dispute not found or access denied" });

        return Ok(result);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyDisputes()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { message = "User not authenticated" });

        var result = await _disputeService.GetMyDisputesAsync(user.Id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> RespondToDispute(
        Guid id,
        [FromBody] RespondToDisputeRequest request
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request", errors = ModelState });

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { message = "User not authenticated" });

        var result = await _disputeService.RespondToDisputeAsync(id, request, user.Id);
        if (result == null)
            return BadRequest(
                new
                {
                    message = "Failed to respond. Ensure you are the respondent and the dispute is awaiting response.",
                }
            );

        return Ok(new { message = "Response submitted", dispute = result });
    }

    [HttpPost("{id:guid}/evidence")]
    public async Task<IActionResult> AddEvidence(Guid id, [FromBody] EvidenceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request", errors = ModelState });

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { message = "User not authenticated" });

        var result = await _disputeService.AddEvidenceAsync(id, request, user.Id);
        if (result == null)
            return BadRequest(
                new
                {
                    message = "Failed to add evidence. Ensure you are part of the dispute and it is still open.",
                }
            );

        return Ok(new { message = "Evidence added", dispute = result });
    }

    [HttpGet("moderation")]
    public async Task<IActionResult> GetDisputesForModeration()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { message = "User not authenticated" });

        if (!user.IsModerator)
            return Forbid();

        var result = await _disputeService.GetDisputesForModerationAsync(user.Id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/moderate")]
    public async Task<IActionResult> MakeModeratorDecision(
        Guid id,
        [FromBody] ModeratorDecisionRequest request
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request", errors = ModelState });

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { message = "User not authenticated" });

        if (!user.IsModerator)
            return Forbid();

        var result = await _disputeService.MakeModeratorDecisionAsync(id, request, user.Id);
        if (result == null)
            return BadRequest(
                new
                {
                    message = "Failed to make decision. Ensure the dispute is escalated to moderator.",
                }
            );

        return Ok(new { message = "Decision recorded", dispute = result });
    }
}
