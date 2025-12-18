using System.ComponentModel.DataAnnotations;
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
        ILogger<AgreementsController> logger
    )
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
                return BadRequest(
                    new
                    {
                        message = "Invalid request",
                        errors = ModelState.Values.SelectMany(v => v.Errors),
                    }
                );
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (currentUser.Id != request.RequesterId && currentUser.Id != request.ProviderId)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to create agreement but is not one of the parties (Requester: {RequesterId}, Provider: {ProviderId})",
                    currentUser.Id,
                    request.RequesterId,
                    request.ProviderId
                );
                return Forbid();
            }

            var agreement = await _agreementService.CreateAgreementAsync(
                request.OfferId,
                request.RequesterId,
                request.ProviderId,
                request.Terms,
                request.Milestones
            );

            if (agreement == null)
            {
                return BadRequest(
                    new
                    {
                        message = "Failed to create agreement. Ensure the offer is active, you are authorized (one party must be the offer owner), no active agreement exists, and all users are valid.",
                    }
                );
            }

            _logger.LogInformation("Agreement {AgreementId} created successfully", agreement.Id);
            return CreatedAtAction(nameof(GetAgreement), new { id = agreement.Id }, agreement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agreement");
            return StatusCode(
                500,
                new { message = "An error occurred while creating the agreement" }
            );
        }
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyAgreements(
        [FromQuery] int? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            Models.AgreementStatus? agreementStatus = status.HasValue
                ? (Models.AgreementStatus)status.Value
                : null;

            var result = await _agreementService.GetUserAgreementsAsync(
                currentUser.Id,
                agreementStatus,
                page,
                pageSize
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user agreements");
            return StatusCode(
                500,
                new { message = "An error occurred while retrieving agreements" }
            );
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

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var agreementDetail = await _agreementService.GetAgreementDetailByIdAsync(id);
            if (agreementDetail == null)
            {
                return NotFound(new { message = "Agreement not found" });
            }

            if (
                agreementDetail.RequesterId != currentUser.Id
                && agreementDetail.ProviderId != currentUser.Id
                && !currentUser.IsModerator
            )
            {
                _logger.LogWarning(
                    "User {UserId} attempted to access agreement {AgreementId} without authorization",
                    currentUser.Id,
                    id
                );
                return Forbid();
            }

            return Ok(agreementDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agreement {AgreementId}", id);
            return StatusCode(
                500,
                new { message = "An error occurred while retrieving the agreement" }
            );
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
                return BadRequest(
                    new
                    {
                        message = "Failed to complete agreement. You may not be authorized or the agreement may already be completed.",
                    }
                );
            }

            _logger.LogInformation(
                "Agreement {AgreementId} completed successfully by user {UserId}",
                id,
                currentUser.Id
            );
            return Ok(
                new
                {
                    success = true,
                    message = "Agreement completed successfully. Offer status updated to Completed.",
                    agreement,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing agreement {AgreementId}", id);
            return StatusCode(
                500,
                new { message = "An error occurred while completing the agreement" }
            );
        }
    }
}

public class CreateAgreementRequest
{
    [Required]
    public Guid OfferId { get; set; }

    [Required]
    public Guid RequesterId { get; set; }

    [Required]
    public Guid ProviderId { get; set; }

    public string? Terms { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one milestone is required")]
    public List<CreateMilestoneRequest> Milestones { get; set; } = new List<CreateMilestoneRequest>();
}
