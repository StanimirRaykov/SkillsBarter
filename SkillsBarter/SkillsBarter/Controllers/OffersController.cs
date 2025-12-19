using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OffersController : ControllerBase
{
    private readonly IOfferService _offerService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OffersController> _logger;

    public OffersController(
        IOfferService offerService,
        UserManager<ApplicationUser> userManager,
        ILogger<OffersController> logger)
    {
        _offerService = offerService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetOffers([FromQuery] GetOffersRequest? request)
    {
        try
        {
            var filters = request ?? new GetOffersRequest();
            _logger.LogInformation("GetOffers called with: Q={Q}, SkillId={SkillId}, Skill={Skill}, Page={Page}, PageSize={PageSize}",
                filters.Q, filters.SkillId, filters.Skill, filters.Page, filters.PageSize);
            var offers = await _offerService.GetOffersAsync(filters);
            _logger.LogInformation("Returning {Count} offers", offers.Items?.Count ?? 0);
            return Ok(offers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching offers with filters {@Filters}", request);
            return StatusCode(500, new { message = "An error occurred while retrieving offers" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOfferById(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Invalid offer ID provided");
                return BadRequest(new { message = "Invalid offer ID" });
            }

            var offer = await _offerService.GetOfferByIdAsync(id);

            if (offer == null)
            {
                _logger.LogWarning("Offer {OfferId} not found or not available", id);
                return NotFound(new { message = "Offer not found" });
            }

            return Ok(offer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching offer {OfferId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the offer" });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateOffer([FromBody] CreateOfferRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for create offer request");
                return BadRequest(new { message = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors) });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("Authenticated user not found");
                return Unauthorized(new { message = "User not found" });
            }

            var offerResponse = await _offerService.CreateOfferAsync(user.Id, request);
            if (offerResponse == null)
            {
                return BadRequest(new { message = "Failed to create offer. Please check your input and try again." });
            }

            _logger.LogInformation("Offer created successfully by user {UserId}", user.Id);
            return CreatedAtAction(nameof(GetOfferById), new { id = offerResponse.Id }, offerResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating offer");
            return StatusCode(500, new { message = "An error occurred while creating the offer" });
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateOffer(Guid id, [FromBody] UpdateOfferRequest request)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Invalid offer ID provided for update");
                return BadRequest(new { message = "Invalid offer ID" });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for update offer request");
                return BadRequest(new { message = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors) });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("Authenticated user not found");
                return Unauthorized(new { message = "User not found" });
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, Constants.AppRoles.Admin) || 
                          await _userManager.IsInRoleAsync(user, Constants.AppRoles.Moderator);

            var offerResponse = await _offerService.UpdateOfferAsync(id, user.Id, request, isAdmin);
            if (offerResponse == null)
            {
                _logger.LogWarning("Failed to update offer {OfferId} - not found or unauthorized", id);
                return NotFound(new { message = "Offer not found or you are not authorized to update it" });
            }

            _logger.LogInformation("Offer {OfferId} updated successfully by user {UserId}", id, user.Id);
            return Ok(offerResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating offer {OfferId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the offer" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteOffer(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Invalid offer ID provided for deletion");
                return BadRequest(new { message = "Invalid offer ID" });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("Authenticated user not found");
                return Unauthorized(new { message = "User not found" });
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, Constants.AppRoles.Admin) || 
                          await _userManager.IsInRoleAsync(user, Constants.AppRoles.Moderator);

            var success = await _offerService.DeleteOfferAsync(id, user.Id, isAdmin);
            if (!success)
            {
                _logger.LogWarning("Failed to delete offer {OfferId} - not found or unauthorized", id);
                return NotFound(new { message = "Offer not found or you are not authorized to delete it" });
            }

            _logger.LogInformation("Offer {OfferId} deleted successfully by user {UserId}", id, user.Id);
            return Ok(new { message = "Offer successfully deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting offer {OfferId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the offer" });
        }
    }
}
