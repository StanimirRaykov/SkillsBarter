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
            var offers = await _offerService.GetOffersAsync(filters);
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
            var offer = await _offerService.GetOfferByIdAsync(id);

            if (offer == null)
            {
                _logger.LogWarning("Offer {OfferId} not found", id);
                return NotFound(new { message = "Offer not found" });
            }

            if (offer.StatusCode != "Active")
            {
                _logger.LogInformation("Attempt to access inactive offer {OfferId} with status {Status}", id, offer.StatusCode);
                return NotFound(new { message = "This offer is no longer available" });
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

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { message = "Title is required and cannot be empty" });
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { message = "Description is required and cannot be empty" });
            }

            if (request.SkillId == Guid.Empty)
            {
                return BadRequest(new { message = "Valid skill ID is required" });
            }

            var offerResponse = await _offerService.CreateOfferAsync(user.Id, request);
            if (offerResponse == null)
            {
                return BadRequest(new { message = "Failed to create offer. Please check your input and try again." });
            }

            _logger.LogInformation("Offer created successfully by user {UserId}", user.Id);
            return CreatedAtAction(nameof(CreateOffer), new { id = offerResponse.Id }, offerResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating offer");
            return StatusCode(500, new { message = "An error occurred while creating the offer" });
        }
    }
}
