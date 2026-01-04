using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.DTOs;
using SkillsBarter.Services;
using System.Security.Claims;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/milestones")]
[Authorize]
public class MilestonesController : ControllerBase
{
    private readonly IMilestoneService _milestoneService;
    private readonly ILogger<MilestonesController> _logger;

    public MilestonesController(IMilestoneService milestoneService, ILogger<MilestonesController> logger)
    {
        _milestoneService = milestoneService;
        _logger = logger;
    }

    [HttpPost("agreement/{agreementId}")]
    public async Task<IActionResult> CreateMilestone(Guid agreementId, [FromBody] CreateMilestoneRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _milestoneService.CreateMilestoneAsync(agreementId, request, userId);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to create milestone. You may not have permission for this agreement." });
        }

        return CreatedAtAction(nameof(GetMilestone), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMilestone(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _milestoneService.GetMilestoneByIdAsync(id, userId);
        if (result == null)
        {
            return NotFound(new { message = "Milestone not found or you don't have permission to view it" });
        }

        return Ok(result);
    }

    [HttpGet("agreement/{agreementId}")]
    public async Task<IActionResult> GetMilestonesByAgreement(Guid agreementId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _milestoneService.GetMilestonesByAgreementIdAsync(agreementId, userId);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMilestone(Guid id, [FromBody] UpdateMilestoneRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _milestoneService.UpdateMilestoneAsync(id, request, userId);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to update milestone. You may not have permission." });
        }

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMilestone(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _milestoneService.DeleteMilestoneAsync(id, userId);
        if (!result)
        {
            return BadRequest(new { message = "Failed to delete milestone. You may not have permission." });
        }

        return NoContent();
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteMilestone(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _milestoneService.MarkMilestoneAsCompletedAsync(id, userId);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to mark milestone as completed. You may not have permission." });
        }

        return Ok(result);
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
