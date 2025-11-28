using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.DTOs;
using SkillsBarter.Services;
using System.Security.Claims;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        IReviewService reviewService,
        ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for create review request");
                return BadRequest(new { message = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors) });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var reviewerId))
            {
                _logger.LogWarning("Create review failed: Invalid user ID in token");
                return Unauthorized(new { message = "Invalid user authentication" });
            }

            var reviewResponse = await _reviewService.CreateReviewAsync(reviewerId, request);
            if (reviewResponse == null)
            {
                return BadRequest(new { message = "Failed to create review. This could be due to: invalid recipient/offer, duplicate review, or attempting to review yourself." });
            }

            _logger.LogInformation("Review created successfully: {ReviewId} by reviewer {ReviewerId}", reviewResponse.Id, reviewerId);
            return CreatedAtAction(nameof(GetReviewsForUser), new { userId = request.RecipientId }, reviewResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review");
            return StatusCode(500, new { message = "An error occurred while creating the review" });
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetReviewsForUser(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var reviews = await _reviewService.GetReviewsForUserAsync(userId, page, pageSize);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for user: {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving reviews" });
        }
    }
}
