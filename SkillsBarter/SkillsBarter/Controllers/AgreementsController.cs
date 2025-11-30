using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgreementsController : ControllerBase
{
    private readonly IAgreementService _agreementService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AgreementsController> _logger;

    public AgreementsController(
        IAgreementService agreementService,
        UserManager<ApplicationUser> userManager,
        ILogger<AgreementsController> logger)
    {
        _agreementService = agreementService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateAgreement([FromBody] CreateAgreementRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors) });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var agreement = await _agreementService.CreateAgreementAsync(
                request.OfferId,
                request.RequesterId,
                request.ProviderId);

            if (agreement == null)
            {
                return BadRequest(new { message = "Failed to create agreement. Please verify the offer is active and all users exist." });
            }

            _logger.LogInformation("Agreement {AgreementId} created successfully", agreement.Id);
            return CreatedAtAction(nameof(GetAgreement), new { id = agreement.Id }, agreement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agreement");
            return StatusCode(500, new { message = "An error occurred while creating the agreement" });
        }
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetAgreement(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid agreement ID" });
            }

            var agreement = await _agreementService.GetAgreementByIdAsync(id);
            if (agreement == null)
            {
                return NotFound(new { message = "Agreement not found" });
            }

            return Ok(agreement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agreement {AgreementId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the agreement" });
        }
    }

    [HttpPut("{id}/complete")]
    [Authorize]
    public async Task<IActionResult> CompleteAgreement(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid agreement ID" });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var agreement = await _agreementService.CompleteAgreementAsync(id, currentUser.Id);
            if (agreement == null)
            {
                return BadRequest(new { message = "Failed to complete agreement. You may not be authorized or the agreement may already be completed." });
            }

            _logger.LogInformation("Agreement {AgreementId} completed successfully by user {UserId}", id, currentUser.Id);
            return Ok(new
            {
                success = true,
                message = "Agreement completed successfully. Offer status updated to Completed.",
                agreement
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing agreement {AgreementId}", id);
            return StatusCode(500, new { message = "An error occurred while completing the agreement" });
        }
    }
}

public class CreateAgreementRequest
{
    public Guid OfferId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid ProviderId { get; set; }
}
