using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Services;

public class ReviewServiceTests
{
    private readonly Mock<ILogger<ReviewService>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotificationService;
    private ApplicationDbContext _context = null!;
    private ReviewService _reviewService = null!;

    public ReviewServiceTests()
    {
        _mockLogger = new Mock<ILogger<ReviewService>>();
        _mockNotificationService = new Mock<INotificationService>();
        SetupInMemoryDatabase();
    }

    private void SetupInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _reviewService = new ReviewService(_context, _mockNotificationService.Object, _mockLogger.Object);
    }

    private async Task<(ApplicationUser reviewer, ApplicationUser recipient, Agreement agreement)> SetupTestDataAsync()
    {
        var reviewer = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Reviewer User",
            UserName = "reviewer",
            Email = "reviewer@example.com"
        };
        _context.Users.Add(reviewer);

        var recipient = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Recipient User",
            UserName = "recipient",
            Email = "recipient@example.com",
            ReputationScore = 0
        };
        _context.Users.Add(recipient);

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            RequesterId = reviewer.Id,
            ProviderId = recipient.Id,
            OfferId = Guid.NewGuid(),
            Status = AgreementStatus.Completed,
            CompletedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.Agreements.Add(agreement);

        await _context.SaveChangesAsync();

        return (reviewer, recipient, agreement);
    }

    [Fact]
    public async Task CreateReviewAsync_WithValidData_ReturnsReviewResponse()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();
        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5,
            Body = "Great experience!"
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.NotNull(result);
        Assert.Equal(recipient.Id, result.RecipientId);
        Assert.Equal(recipient.Name, result.RecipientName);
        Assert.Equal(reviewer.Id, result.ReviewerId);
        Assert.Equal(reviewer.Name, result.ReviewerName);
        Assert.Equal(agreement.Id, result.AgreementId);
        Assert.Equal(5, result.Rating);
        Assert.Equal("Great experience!", result.Body);

        var updatedRecipient = await _context.Users.FindAsync(recipient.Id);
        Assert.Equal(5m, updatedRecipient!.ReputationScore);
    }

    [Fact]
    public async Task CreateReviewAsync_ReviewerNotFound_ReturnsNull()
    {
        var (_, recipient, agreement) = await SetupTestDataAsync();
        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(Guid.NewGuid(), request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_RecipientNotFound_ReturnsNull()
    {
        var (reviewer, _, agreement) = await SetupTestDataAsync();
        var request = new CreateReviewRequest
        {
            RecipientId = Guid.NewGuid(),
            AgreementId = agreement.Id,
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_AgreementNotFound_ReturnsNull()
    {
        var (reviewer, recipient, _) = await SetupTestDataAsync();
        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = Guid.NewGuid(),
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_AgreementNotCompleted_ReturnsNull()
    {
        var (reviewer, recipient, _) = await SetupTestDataAsync();

        var pendingAgreement = new Agreement
        {
            Id = Guid.NewGuid(),
            RequesterId = reviewer.Id,
            ProviderId = recipient.Id,
            OfferId = Guid.NewGuid(),
            Status = AgreementStatus.Pending,
            CompletedAt = null
        };
        _context.Agreements.Add(pendingAgreement);
        await _context.SaveChangesAsync();

        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = pendingAgreement.Id,
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_AgreementCompletedAtNull_ReturnsNull()
    {
        var (reviewer, recipient, _) = await SetupTestDataAsync();

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            RequesterId = reviewer.Id,
            ProviderId = recipient.Id,
            OfferId = Guid.NewGuid(),
            Status = AgreementStatus.Completed,
            CompletedAt = null
        };
        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_AgreementStatusNotCompleted_ReturnsNull()
    {
        var (reviewer, recipient, _) = await SetupTestDataAsync();

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            RequesterId = reviewer.Id,
            ProviderId = recipient.Id,
            OfferId = Guid.NewGuid(),
            Status = AgreementStatus.Cancelled,
            CompletedAt = DateTime.UtcNow
        };
        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_ReviewerNotPartOfAgreement_ReturnsNull()
    {
        var (_, recipient, _) = await SetupTestDataAsync();

        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Other User",
            UserName = "other",
            Email = "other@example.com"
        };
        _context.Users.Add(otherUser);

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            RequesterId = otherUser.Id,
            ProviderId = recipient.Id,
            OfferId = Guid.NewGuid(),
            Status = AgreementStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };
        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(Guid.NewGuid(), request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_RecipientNotPartOfAgreement_ReturnsNull()
    {
        var (reviewer, _, _) = await SetupTestDataAsync();

        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Other User",
            UserName = "other",
            Email = "other@example.com"
        };
        _context.Users.Add(otherUser);

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            RequesterId = reviewer.Id,
            ProviderId = otherUser.Id,
            OfferId = Guid.NewGuid(),
            Status = AgreementStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };
        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        var request = new CreateReviewRequest
        {
            RecipientId = Guid.NewGuid(),
            AgreementId = agreement.Id,
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_UserReviewsThemselves_ReturnsNull()
    {
        var (reviewer, _, _) = await SetupTestDataAsync();

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            RequesterId = reviewer.Id,
            ProviderId = reviewer.Id,
            OfferId = Guid.NewGuid(),
            Status = AgreementStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };
        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        var request = new CreateReviewRequest
        {
            RecipientId = reviewer.Id,
            AgreementId = agreement.Id,
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_DuplicateReview_ReturnsNull()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var existingReview = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = reviewer.Id,
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 4,
            Body = "First review"
        };
        _context.Reviews.Add(existingReview);
        await _context.SaveChangesAsync();

        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5,
            Body = "Second review"
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReviewAsync_UpdatesRecipientReputationScore()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var otherReviewer = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Other Reviewer",
            UserName = "otherreviewer",
            Email = "otherreviewer@example.com"
        };
        _context.Users.Add(otherReviewer);

        var otherAgreement = new Agreement
        {
            Id = Guid.NewGuid(),
            RequesterId = otherReviewer.Id,
            ProviderId = recipient.Id,
            OfferId = Guid.NewGuid(),
            Status = AgreementStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };
        _context.Agreements.Add(otherAgreement);

        var existingReview = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = otherReviewer.Id,
            RecipientId = recipient.Id,
            AgreementId = otherAgreement.Id,
            Rating = 4
        };
        _context.Reviews.Add(existingReview);
        await _context.SaveChangesAsync();

        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 2
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.NotNull(result);

        var updatedRecipient = await _context.Users.FindAsync(recipient.Id);
        Assert.Equal(3m, updatedRecipient!.ReputationScore);
    }

    [Fact]
    public async Task CreateReviewAsync_WithNullBody_CreatesReview()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();
        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5,
            Body = null
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.NotNull(result);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task CreateReviewAsync_ReviewerAsProvider_CanReviewRequester()
    {
        var (reviewer, recipient, _) = await SetupTestDataAsync();

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            RequesterId = reviewer.Id,
            ProviderId = recipient.Id,
            OfferId = Guid.NewGuid(),
            Status = AgreementStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            Requester = reviewer,
            Provider = recipient
        };
        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        var request = new CreateReviewRequest
        {
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5
        };

        var result = await _reviewService.CreateReviewAsync(reviewer.Id, request);

        Assert.NotNull(result);
        Assert.Equal(recipient.Id, result.RecipientId);
        Assert.Equal(reviewer.Id, result.ReviewerId);
    }

    [Fact]
    public async Task GetReviewsForUserAsync_ReturnsPaginatedReviews()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var reviews = new List<Review>
        {
            new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewer.Id,
                RecipientId = recipient.Id,
                AgreementId = agreement.Id,
                Rating = 5,
                Body = "Great!",
                CreatedAt = DateTime.UtcNow
            },
            new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewer.Id,
                RecipientId = recipient.Id,
                AgreementId = agreement.Id,
                Rating = 4,
                Body = "Good",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };
        _context.Reviews.AddRange(reviews);
        await _context.SaveChangesAsync();

        var result = await _reviewService.GetReviewsForUserAsync(recipient.Id, 1, 10);

        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task GetReviewsForUserAsync_OrdersByCreatedAtDescending()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var baseTime = DateTime.UtcNow;
        var reviews = new List<Review>
        {
            new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewer.Id,
                RecipientId = recipient.Id,
                AgreementId = agreement.Id,
                Rating = 3,
                Body = "Oldest",
                CreatedAt = baseTime.AddDays(-2)
            },
            new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewer.Id,
                RecipientId = recipient.Id,
                AgreementId = agreement.Id,
                Rating = 5,
                Body = "Newest",
                CreatedAt = baseTime
            },
            new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewer.Id,
                RecipientId = recipient.Id,
                AgreementId = agreement.Id,
                Rating = 4,
                Body = "Middle",
                CreatedAt = baseTime.AddDays(-1)
            }
        };
        _context.Reviews.AddRange(reviews);
        await _context.SaveChangesAsync();

        var result = await _reviewService.GetReviewsForUserAsync(recipient.Id, 1, 10);

        Assert.Equal("Newest", result.Items[0].Body);
        Assert.Equal("Middle", result.Items[1].Body);
        Assert.Equal("Oldest", result.Items[2].Body);
    }

    [Fact]
    public async Task GetReviewsForUserAsync_WithPagination_ReturnsCorrectPage()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var reviews = Enumerable.Range(1, 15).Select(i => new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = reviewer.Id,
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5,
            Body = $"Review {i}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();
        _context.Reviews.AddRange(reviews);
        await _context.SaveChangesAsync();

        var result = await _reviewService.GetReviewsForUserAsync(recipient.Id, 2, 5);

        Assert.NotNull(result);
        Assert.Equal(15, result.Total);
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
    }

    [Fact]
    public async Task GetReviewsForUserAsync_WithInvalidPaging_AdjustsToDefaults()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = reviewer.Id,
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5
        };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var result = await _reviewService.GetReviewsForUserAsync(recipient.Id, 0, 0);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task GetReviewsForUserAsync_WithPageSizeOver100_CapsAt100()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = reviewer.Id,
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5
        };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var result = await _reviewService.GetReviewsForUserAsync(recipient.Id, 1, 150);

        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task GetReviewsForUserAsync_NoReviews_ReturnsEmptyList()
    {
        var (_, recipient, _) = await SetupTestDataAsync();

        var result = await _reviewService.GetReviewsForUserAsync(recipient.Id, 1, 10);

        Assert.NotNull(result);
        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetReviewsForUserAsync_OnlyReturnsReviewsForSpecificUser()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Other User",
            UserName = "other",
            Email = "other@example.com"
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var reviews = new List<Review>
        {
            new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewer.Id,
                RecipientId = recipient.Id,
                AgreementId = agreement.Id,
                Rating = 5
            },
            new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewer.Id,
                RecipientId = otherUser.Id,
                AgreementId = agreement.Id,
                Rating = 4
            }
        };
        _context.Reviews.AddRange(reviews);
        await _context.SaveChangesAsync();

        var result = await _reviewService.GetReviewsForUserAsync(recipient.Id, 1, 10);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.All(result.Items, r => Assert.Equal(recipient.Id, r.RecipientId));
    }

    [Fact]
    public async Task GetUserReviewsWithSummaryAsync_ReturnsSummaryAndReviews()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var reviews = new List<Review>
        {
            new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewer.Id,
                RecipientId = recipient.Id,
                AgreementId = agreement.Id,
                Rating = 5,
                Body = "Excellent"
            },
            new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewer.Id,
                RecipientId = recipient.Id,
                AgreementId = agreement.Id,
                Rating = 3,
                Body = "Average"
            }
        };
        _context.Reviews.AddRange(reviews);
        await _context.SaveChangesAsync();

        var result = await _reviewService.GetUserReviewsWithSummaryAsync(recipient.Id, 1, 10);

        Assert.NotNull(result);
        Assert.Equal(2, result.Summary.TotalReviews);
        Assert.Equal(4m, result.Summary.AverageRating);
        Assert.Equal(2, result.Reviews.Total);
        Assert.Equal(2, result.Reviews.Items.Count);
    }

    [Fact]
    public async Task GetUserReviewsWithSummaryAsync_NoReviews_ReturnsZeroAverageRating()
    {
        var (_, recipient, _) = await SetupTestDataAsync();

        var result = await _reviewService.GetUserReviewsWithSummaryAsync(recipient.Id, 1, 10);

        Assert.NotNull(result);
        Assert.Equal(0, result.Summary.TotalReviews);
        Assert.Equal(0m, result.Summary.AverageRating);
        Assert.Empty(result.Reviews.Items);
    }

    [Fact]
    public async Task GetUserReviewsWithSummaryAsync_WithPagination_ReturnsPaginatedReviews()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var reviews = Enumerable.Range(1, 15).Select(i => new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = reviewer.Id,
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5,
            CreatedAt = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();
        _context.Reviews.AddRange(reviews);
        await _context.SaveChangesAsync();

        var result = await _reviewService.GetUserReviewsWithSummaryAsync(recipient.Id, 2, 5);

        Assert.NotNull(result);
        Assert.Equal(15, result.Summary.TotalReviews);
        Assert.Equal(5m, result.Summary.AverageRating);
        Assert.Equal(15, result.Reviews.Total);
        Assert.Equal(5, result.Reviews.Items.Count);
        Assert.Equal(2, result.Reviews.Page);
    }

    [Fact]
    public async Task GetUserReviewsWithSummaryAsync_WithInvalidPaging_AdjustsToDefaults()
    {
        var (reviewer, recipient, agreement) = await SetupTestDataAsync();

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = reviewer.Id,
            RecipientId = recipient.Id,
            AgreementId = agreement.Id,
            Rating = 5
        };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var result = await _reviewService.GetUserReviewsWithSummaryAsync(recipient.Id, -1, 200);

        Assert.Equal(1, result.Reviews.Page);
        Assert.Equal(100, result.Reviews.PageSize);
    }
}
