using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(ApplicationDbContext dbContext, ILogger<ReviewService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ReviewResponse?> CreateReviewAsync(Guid reviewerId, CreateReviewRequest request)
    {
        try
        {
            var reviewer = await _dbContext.Users.FindAsync(reviewerId);
            if (reviewer == null)
            {
                _logger.LogWarning("Create review failed: Reviewer {ReviewerId} not found", reviewerId);
                return null;
            }

            var recipient = await _dbContext.Users.FindAsync(request.RecipientId);
            if (recipient == null)
            {
                _logger.LogWarning("Create review failed: Recipient {RecipientId} not found", request.RecipientId);
                return null;
            }

            var agreement = await _dbContext.Agreements
                .Include(a => a.Buyer)
                .Include(a => a.Seller)
                .FirstOrDefaultAsync(a => a.Id == request.AgreementId);

            if (agreement == null)
            {
                _logger.LogWarning("Create review failed: Agreement {AgreementId} not found", request.AgreementId);
                return null;
            }

            if (agreement.BuyerId != reviewerId && agreement.SellerId != reviewerId)
            {
                _logger.LogWarning("Create review failed: Reviewer {ReviewerId} is not part of agreement {AgreementId}",
                    reviewerId, request.AgreementId);
                return null;
            }

            if (agreement.BuyerId != request.RecipientId && agreement.SellerId != request.RecipientId)
            {
                _logger.LogWarning("Create review failed: Recipient {RecipientId} is not part of agreement {AgreementId}",
                    request.RecipientId, request.AgreementId);
                return null;
            }

            if (reviewerId == request.RecipientId)
            {
                _logger.LogWarning("Create review failed: User cannot review themselves");
                return null;
            }

            var existingReview = await _dbContext.Reviews
                .FirstOrDefaultAsync(r => r.ReviewerId == reviewerId &&
                                         r.AgreementId == request.AgreementId &&
                                         r.RecipientId == request.RecipientId);
            if (existingReview != null)
            {
                _logger.LogWarning("Create review failed: Review already exists for agreement {AgreementId} and recipient {RecipientId}",
                    request.AgreementId, request.RecipientId);
                return null;
            }

            var review = new Review
            {
                Id = Guid.NewGuid(),
                ReviewerId = reviewerId,
                RecipientId = request.RecipientId,
                AgreementId = request.AgreementId,
                Rating = request.Rating,
                Body = request.Body,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Reviews.Add(review);

            var allRecipientReviews = await _dbContext.Reviews
                .Where(r => r.RecipientId == request.RecipientId)
                .ToListAsync();

            allRecipientReviews.Add(review);

            var averageRating = allRecipientReviews.Average(r => r.Rating);
            recipient.ReputationScore = (decimal)averageRating;
            recipient.UpdatedAt = DateTime.UtcNow;

            _dbContext.Users.Update(recipient);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Review created successfully: {ReviewId}. Recipient {RecipientId} new reputation score: {ReputationScore}",
                review.Id, recipient.Id, recipient.ReputationScore);

            return MapToReviewResponse(review, recipient.Name, reviewer.Name, request.AgreementId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review");
            throw;
        }
    }

    public async Task<PaginatedResponse<ReviewResponse>> GetReviewsForUserAsync(Guid userId, int page = 1, int pageSize = 10)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _dbContext.Reviews
                .Include(r => r.Recipient)
                .Include(r => r.Reviewer)
                .Where(r => r.RecipientId == userId)
                .OrderByDescending(r => r.CreatedAt);

            var total = await query.CountAsync();

            var reviews = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var reviewResponses = reviews.Select(r =>
                MapToReviewResponse(r, r.Recipient.Name, r.Reviewer.Name, r.AgreementId)).ToList();

            return new PaginatedResponse<ReviewResponse>
            {
                Items = reviewResponses,
                Page = page,
                PageSize = pageSize,
                Total = total
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for user: {UserId}", userId);
            throw;
        }
    }

    private ReviewResponse MapToReviewResponse(Review review, string recipientName, string reviewerName, Guid agreementId)
    {
        return new ReviewResponse
        {
            Id = review.Id,
            RecipientId = review.RecipientId,
            RecipientName = recipientName,
            ReviewerId = review.ReviewerId,
            ReviewerName = reviewerName,
            AgreementId = agreementId,
            Rating = review.Rating,
            Body = review.Body,
            CreatedAt = review.CreatedAt
        };
    }
}
