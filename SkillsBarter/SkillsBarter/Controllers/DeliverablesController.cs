using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            var message = errors.Any() ? string.Join(" ", errors) : "Invalid request. Please correct the highlighted fields.";
            return BadRequest(new { message, errors = ModelState });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        try
        {
            var result = await _deliverableService.SubmitDeliverableAsync(request, user.Id);
            return CreatedAtAction(nameof(GetDeliverable), new { id = result!.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error submitting deliverable");
            return BadRequest(new { message = "Could not submit deliverable due to a data constraint. Please check for existing submissions for this agreement." });
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error submitting deliverable");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unexpected error submitting deliverable." });
        }
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

        try
        {
            var result = await _deliverableService.ApproveDeliverableAsync(id, user.Id);
            return Ok(new { message = "Deliverable approved", deliverable = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error approving deliverable {DeliverableId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unexpected error approving deliverable." });
        }

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

        try
        {
            var result = await _deliverableService.RequestRevisionAsync(id, request, user.Id);
            return Ok(new { message = "Revision requested", deliverable = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error requesting revision for deliverable {DeliverableId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unexpected error requesting revision." });
        }

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

        try
        {
            var result = await _deliverableService.ResubmitDeliverableAsync(id, request, user.Id);
            return Ok(new { message = "Deliverable resubmitted", deliverable = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error resubmitting deliverable {DeliverableId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unexpected error resubmitting deliverable." });
        }

    }
}
