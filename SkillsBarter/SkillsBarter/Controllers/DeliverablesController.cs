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
public class DeliverablesController : ControllerBase
{
    private readonly IDeliverableService _deliverableService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DeliverablesController> _logger;

    public DeliverablesController(
        IDeliverableService deliverableService,
        UserManager<ApplicationUser> userManager,
        ILogger<DeliverablesController> logger)
    {
        _deliverableService = deliverableService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitDeliverable([FromBody] SubmitDeliverableRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            var message = errors.Any() ? string.Join(" ", errors) : "Invalid request";
            return BadRequest(new { message, errors = ModelState });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _deliverableService.SubmitDeliverableAsync(request, user.Id);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to submit deliverable. Ensure the agreement exists, is in progress, and you haven't already submitted." });
        }

        return CreatedAtAction(nameof(GetDeliverable), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDeliverable(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _deliverableService.GetDeliverableByIdAsync(id, user.Id);
        if (result == null)
        {
            return NotFound(new { message = "Deliverable not found or access denied" });
        }

        return Ok(result);
    }

    [HttpGet("agreement/{agreementId:guid}")]
    public async Task<IActionResult> GetAgreementDeliverables(Guid agreementId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _deliverableService.GetAgreementDeliverablesAsync(agreementId, user.Id);
        if (result == null)
        {
            return NotFound(new { message = "Agreement not found or access denied" });
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveDeliverable(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _deliverableService.ApproveDeliverableAsync(id, user.Id);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to approve. Ensure the deliverable exists, is submitted, and you're the other party." });
        }

        return Ok(new { message = "Deliverable approved", deliverable = result });
    }

    [HttpPost("{id:guid}/request-revision")]
    public async Task<IActionResult> RequestRevision(Guid id, [FromBody] RequestRevisionRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            var message = errors.Any() ? string.Join(" ", errors) : "Invalid request";
            return BadRequest(new { message, errors = ModelState });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _deliverableService.RequestRevisionAsync(id, request, user.Id);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to request revision. Ensure the deliverable exists, is submitted, and you're the other party." });
        }

        return Ok(new { message = "Revision requested", deliverable = result });
    }

    [HttpPut("{id:guid}/resubmit")]
    public async Task<IActionResult> ResubmitDeliverable(Guid id, [FromBody] SubmitDeliverableRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            var message = errors.Any() ? string.Join(" ", errors) : "Invalid request";
            return BadRequest(new { message, errors = ModelState });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _deliverableService.ResubmitDeliverableAsync(id, request, user.Id);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to resubmit. Ensure the deliverable exists, revision was requested, and you're the submitter." });
        }

        return Ok(new { message = "Deliverable resubmitted", deliverable = result });
    }
}
