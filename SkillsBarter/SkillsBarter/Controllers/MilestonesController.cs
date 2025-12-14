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
        var result = await _milestoneService.CreateMilestoneAsync(agreementId, request);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to create milestone" });
        }

        return CreatedAtAction(nameof(GetMilestone), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMilestone(Guid id)
    {
        var result = await _milestoneService.GetMilestoneByIdAsync(id);
        if (result == null)
        {
            return NotFound(new { message = "Milestone not found" });
        }

        return Ok(result);
    }

    [HttpGet("agreement/{agreementId}")]
    public async Task<IActionResult> GetMilestonesByAgreement(Guid agreementId)
    {
        var result = await _milestoneService.GetMilestonesByAgreementIdAsync(agreementId);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMilestone(Guid id, [FromBody] UpdateMilestoneRequest request)
    {
        var result = await _milestoneService.UpdateMilestoneAsync(id, request);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to update milestone" });
        }

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMilestone(Guid id)
    {
        var result = await _milestoneService.DeleteMilestoneAsync(id);
        if (!result)
        {
            return BadRequest(new { message = "Failed to delete milestone" });
        }

        return NoContent();
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteMilestone(Guid id)
    {
        var result = await _milestoneService.MarkMilestoneAsCompletedAsync(id);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to mark milestone as completed" });
        }

        return Ok(result);
    }
}
