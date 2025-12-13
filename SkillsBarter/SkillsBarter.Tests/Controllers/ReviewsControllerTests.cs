using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Controllers;
using SkillsBarter.DTOs;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Controllers;

public class ReviewsControllerTests
{
    private readonly Mock<IReviewService> _reviewServiceMock = new();
    private readonly Mock<ILogger<ReviewsController>> _loggerMock = new();
    private readonly ReviewsController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public ReviewsControllerTests()
    {
        _controller = new ReviewsController(
            _reviewServiceMock.Object,
            _loggerMock.Object);

        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task CreateReview_WithValidRequest_ReturnsCreatedAtAction()
    {
        var recipientId = Guid.NewGuid();
        var agreementId = Guid.NewGuid();
        var request = new CreateReviewRequest
        {
            RecipientId = recipientId,
            AgreementId = agreementId,
            Rating = 5,
            Body = "Great experience!"
        };

        var expectedResponse = new ReviewResponse
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            RecipientName = "Recipient",
            ReviewerId = _testUserId,
            ReviewerName = "Reviewer",
            AgreementId = agreementId,
            Rating = 5,
            Body = "Great experience!",
            CreatedAt = DateTime.UtcNow
        };

        _reviewServiceMock.Setup(s => s.CreateReviewAsync(_testUserId, request))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.CreateReview(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ReviewsController.GetReviewsForUser), createdResult.ActionName);
        Assert.Equal(recipientId, createdResult.RouteValues?["userId"]);
        Assert.Equal(expectedResponse, createdResult.Value);
    }

    [Fact]
    public async Task CreateReview_WithInvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Rating", "Rating is required");
        var request = new CreateReviewRequest
        {
            RecipientId = Guid.NewGuid(),
            AgreementId = Guid.NewGuid(),
            Rating = 0
        };

        var result = await _controller.CreateReview(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
        _reviewServiceMock.Verify(s => s.CreateReviewAsync(It.IsAny<Guid>(), It.IsAny<CreateReviewRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateReview_WithMissingUserIdClaim_ReturnsUnauthorized()
    {
        var controller = new ReviewsController(_reviewServiceMock.Object, _loggerMock.Object);
        var claims = new List<Claim>();
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        var request = new CreateReviewRequest
        {
            RecipientId = Guid.NewGuid(),
            AgreementId = Guid.NewGuid(),
            Rating = 5
        };

        var result = await controller.CreateReview(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid user authentication", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task CreateReview_WithInvalidUserIdFormat_ReturnsUnauthorized()
    {
        var controller = new ReviewsController(_reviewServiceMock.Object, _loggerMock.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "invalid-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        var request = new CreateReviewRequest
        {
            RecipientId = Guid.NewGuid(),
            AgreementId = Guid.NewGuid(),
            Rating = 5
        };

        var result = await controller.CreateReview(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid user authentication", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task CreateReview_ServiceReturnsNull_ReturnsBadRequest()
    {
        var request = new CreateReviewRequest
        {
            RecipientId = Guid.NewGuid(),
            AgreementId = Guid.NewGuid(),
            Rating = 5
        };

        _reviewServiceMock.Setup(s => s.CreateReviewAsync(_testUserId, request))
            .ReturnsAsync((ReviewResponse?)null);

        var result = await _controller.CreateReview(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to create review", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task CreateReview_OnException_ReturnsServerError()
    {
        var request = new CreateReviewRequest
        {
            RecipientId = Guid.NewGuid(),
            AgreementId = Guid.NewGuid(),
            Rating = 5
        };

        _reviewServiceMock.Setup(s => s.CreateReviewAsync(_testUserId, request))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.CreateReview(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while creating the review", GetMessage(objectResult.Value));
    }

    [Fact]
    public async Task CreateReview_WithNullBody_CreatesReview()
    {
        var recipientId = Guid.NewGuid();
        var agreementId = Guid.NewGuid();
        var request = new CreateReviewRequest
        {
            RecipientId = recipientId,
            AgreementId = agreementId,
            Rating = 5,
            Body = null
        };

        var expectedResponse = new ReviewResponse
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            RecipientName = "Recipient",
            ReviewerId = _testUserId,
            ReviewerName = "Reviewer",
            AgreementId = agreementId,
            Rating = 5,
            Body = null,
            CreatedAt = DateTime.UtcNow
        };

        _reviewServiceMock.Setup(s => s.CreateReviewAsync(_testUserId, request))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.CreateReview(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<ReviewResponse>(createdResult.Value);
        Assert.Null(response.Body);
    }

    [Fact]
    public async Task GetReviewsForUser_WithValidUserId_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var expectedResponse = new PaginatedResponse<ReviewResponse>
        {
            Items = new List<ReviewResponse>
            {
                new ReviewResponse
                {
                    Id = Guid.NewGuid(),
                    RecipientId = userId,
                    RecipientName = "User",
                    ReviewerId = Guid.NewGuid(),
                    ReviewerName = "Reviewer",
                    AgreementId = Guid.NewGuid(),
                    Rating = 5,
                    Body = "Great!",
                    CreatedAt = DateTime.UtcNow
                }
            },
            Page = 1,
            PageSize = 10,
            Total = 1
        };

        _reviewServiceMock.Setup(s => s.GetReviewsForUserAsync(userId, 1, 10))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetReviewsForUser(userId, 1, 10);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PaginatedResponse<ReviewResponse>>(okResult.Value);
        Assert.Equal(expectedResponse, response);
    }

    [Fact]
    public async Task GetReviewsForUser_WithDefaultPagination_UsesDefaults()
    {
        var userId = Guid.NewGuid();
        var expectedResponse = new PaginatedResponse<ReviewResponse>
        {
            Items = new List<ReviewResponse>(),
            Page = 1,
            PageSize = 10,
            Total = 0
        };

        _reviewServiceMock.Setup(s => s.GetReviewsForUserAsync(userId, 1, 10))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetReviewsForUser(userId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        _reviewServiceMock.Verify(s => s.GetReviewsForUserAsync(userId, 1, 10), Times.Once);
    }

    [Fact]
    public async Task GetReviewsForUser_WithCustomPagination_PassesParameters()
    {
        var userId = Guid.NewGuid();
        var expectedResponse = new PaginatedResponse<ReviewResponse>
        {
            Items = new List<ReviewResponse>(),
            Page = 2,
            PageSize = 20,
            Total = 0
        };

        _reviewServiceMock.Setup(s => s.GetReviewsForUserAsync(userId, 2, 20))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetReviewsForUser(userId, 2, 20);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PaginatedResponse<ReviewResponse>>(okResult.Value);
        Assert.Equal(2, response.Page);
        Assert.Equal(20, response.PageSize);
    }

    [Fact]
    public async Task GetReviewsForUser_NoReviews_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();
        var expectedResponse = new PaginatedResponse<ReviewResponse>
        {
            Items = new List<ReviewResponse>(),
            Page = 1,
            PageSize = 10,
            Total = 0
        };

        _reviewServiceMock.Setup(s => s.GetReviewsForUserAsync(userId, 1, 10))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetReviewsForUser(userId, 1, 10);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PaginatedResponse<ReviewResponse>>(okResult.Value);
        Assert.Empty(response.Items);
        Assert.Equal(0, response.Total);
    }

    [Fact]
    public async Task GetReviewsForUser_OnException_ReturnsServerError()
    {
        var userId = Guid.NewGuid();
        _reviewServiceMock.Setup(s => s.GetReviewsForUserAsync(userId, 1, 10))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetReviewsForUser(userId, 1, 10);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while retrieving reviews", GetMessage(objectResult.Value));
    }

    [Fact]
    public async Task GetReviewsForUser_WithMultipleReviews_ReturnsAll()
    {
        var userId = Guid.NewGuid();
        var expectedResponse = new PaginatedResponse<ReviewResponse>
        {
            Items = new List<ReviewResponse>
            {
                new ReviewResponse
                {
                    Id = Guid.NewGuid(),
                    RecipientId = userId,
                    RecipientName = "User",
                    ReviewerId = Guid.NewGuid(),
                    ReviewerName = "Reviewer 1",
                    AgreementId = Guid.NewGuid(),
                    Rating = 5,
                    CreatedAt = DateTime.UtcNow
                },
                new ReviewResponse
                {
                    Id = Guid.NewGuid(),
                    RecipientId = userId,
                    RecipientName = "User",
                    ReviewerId = Guid.NewGuid(),
                    ReviewerName = "Reviewer 2",
                    AgreementId = Guid.NewGuid(),
                    Rating = 4,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            },
            Page = 1,
            PageSize = 10,
            Total = 2
        };

        _reviewServiceMock.Setup(s => s.GetReviewsForUserAsync(userId, 1, 10))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetReviewsForUser(userId, 1, 10);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PaginatedResponse<ReviewResponse>>(okResult.Value);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(2, response.Total);
    }

    private static string? GetMessage(object? value) =>
        value?.GetType().GetProperty("message")?.GetValue(value)?.ToString();
}
