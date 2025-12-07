using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProposalsController : ControllerBase
{
    private readonly IProposalService _proposalService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ProposalsController> _logger;

    public ProposalsController(
        IProposalService proposalService,
        UserManager<ApplicationUser> userManager,
        ILogger<ProposalsController> logger)
    {
        _proposalService = proposalService;
        _userManager = userManager;
        _logger = logger;
    }


    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateProposal([FromBody] CreateProposalRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var proposal = await _proposalService.CreateProposalAsync(request, currentUser.Id);

            if (proposal == null)
            {
                return BadRequest(new { message = "Failed to create proposal. Ensure the offer is active, you are not the offer owner, deadline is in the future, and you don't already have a pending proposal for this offer." });
            }

            _logger.LogInformation("Proposal {ProposalId} created by user {UserId} for offer {OfferId}",
                proposal.Id, currentUser.Id, request.OfferId);

            return CreatedAtAction(nameof(GetProposal), new { id = proposal.Id }, proposal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating proposal");
            return StatusCode(500, new { message = "An error occurred while creating the proposal" });
        }
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetProposal(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid proposal ID" });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var proposal = await _proposalService.GetProposalDetailByIdAsync(id);
            if (proposal == null)
            {
                return NotFound(new { message = "Proposal not found" });
            }

            // Only allow access to participants or moderators
            if (proposal.ProposerId != currentUser.Id &&
                proposal.OfferOwnerId != currentUser.Id &&
                !currentUser.IsModerator)
            {
                _logger.LogWarning("User {UserId} attempted to access proposal {ProposalId} without authorization",
                    currentUser.Id, id);
                return Forbid();
            }

            return Ok(proposal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving proposal {ProposalId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the proposal" });
        }
    }

    [HttpPost("{id}/respond")]
    [Authorize]
    public async Task<IActionResult> RespondToProposal(Guid id, [FromBody] RespondToProposalRequest request)
    {
        try
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid proposal ID" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Check if user can respond
            var canRespond = await _proposalService.CanUserRespondAsync(id, currentUser.Id);
            if (!canRespond)
            {
                return BadRequest(new { message = "You cannot respond to this proposal. Either it's not your turn, the proposal is not pending, or you are not a participant." });
            }

            var result = await _proposalService.RespondToProposalAsync(id, request, currentUser.Id);

            if (result == null)
            {
                return BadRequest(new { message = "Failed to respond to proposal. Ensure all required fields are provided for modifications." });
            }

            _logger.LogInformation("User {UserId} responded to proposal {ProposalId} with action {Action}",
                currentUser.Id, id, request.Action);

            return Ok(new
            {
                success = true,
                message = request.Action switch
                {
                    ProposalResponseAction.Accept => result.AgreementId.HasValue
                        ? "Proposal accepted. Agreement created."
                        : "Proposal accepted.",
                    ProposalResponseAction.Modify => "Proposal modified. Waiting for other party's response.",
                    ProposalResponseAction.Decline => "Proposal declined.",
                    _ => "Response recorded."
                },
                proposal = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error responding to proposal {ProposalId}", id);
            return StatusCode(500, new { message = "An error occurred while responding to the proposal" });
        }
    }

    [HttpPost("{id}/withdraw")]
    [Authorize]
    public async Task<IActionResult> WithdrawProposal(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid proposal ID" });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _proposalService.WithdrawProposalAsync(id, currentUser.Id);

            if (!success)
            {
                return BadRequest(new { message = "Failed to withdraw proposal. Ensure you are the proposer and the proposal is still pending." });
            }

            _logger.LogInformation("Proposal {ProposalId} withdrawn by user {UserId}", id, currentUser.Id);

            return Ok(new { success = true, message = "Proposal withdrawn successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing proposal {ProposalId}", id);
            return StatusCode(500, new { message = "An error occurred while withdrawing the proposal" });
        }
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyProposals([FromQuery] GetProposalsRequest request)
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var proposals = await _proposalService.GetUserProposalsAsync(currentUser.Id, request);
            return Ok(proposals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user proposals");
            return StatusCode(500, new { message = "An error occurred while retrieving proposals" });
        }
    }

    [HttpGet("offer/{offerId}")]
    [Authorize]
    public async Task<IActionResult> GetOfferProposals(Guid offerId, [FromQuery] GetProposalsRequest request)
    {
        try
        {
            if (offerId == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid offer ID" });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            request.OfferId = offerId;

            var proposals = await _proposalService.GetOfferProposalsAsync(offerId, request);
            return Ok(proposals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving proposals for offer {OfferId}", offerId);
            return StatusCode(500, new { message = "An error occurred while retrieving proposals" });
        }
    }

    [HttpGet("pending")]
    [Authorize]
    public async Task<IActionResult> GetPendingProposals([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var request = new GetProposalsRequest
            {
                Page = page,
                PageSize = pageSize
            };

            var allProposals = await _proposalService.GetUserProposalsAsync(currentUser.Id, new GetProposalsRequest
            {
                Page = 1,
                // Get all proposals to filter pending ones
                PageSize = 1000
            });

            var pendingProposals = allProposals.Proposals
                .Where(p => p.PendingResponseFromUserId == currentUser.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var totalPending = allProposals.Proposals.Count(p => p.PendingResponseFromUserId == currentUser.Id);

            return Ok(new ProposalListResponse
            {
                Proposals = pendingProposals,
                TotalCount = totalPending,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalPending / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending proposals");
            return StatusCode(500, new { message = "An error occurred while retrieving pending proposals" });
        }
    }
}
