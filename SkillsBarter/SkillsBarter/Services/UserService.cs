using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    public UserService(ApplicationDbContext dbContext, ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PublicUserProfileResponse?> GetPublicProfileAsync(Guid userId)
    {
        try
        {
            var user = await _dbContext.Users
                .Include(u => u.Offers.Where(o => o.StatusCode == OfferStatusCode.Active))
                    .ThenInclude(o => o.Skill)
                .Include(u => u.ReviewsReceived)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("User not found with ID: {UserId}", userId);
                return null;
            }

            // Group offers by skill and count active offers per skill
            var skillsWithCounts = user.Offers
                .GroupBy(o => new { o.SkillId, o.Skill.Name, o.Skill.CategoryCode })
                .Select(g => new UserSkillDto
                {
                    SkillId = g.Key.SkillId,
                    SkillName = g.Key.Name,
                    CategoryCode = g.Key.CategoryCode,
                    ActiveOffersCount = g.Count()
                })
                .OrderByDescending(s => s.ActiveOffersCount)
                .ToList();

            // Calculate review statistics
            var reviewsReceived = user.ReviewsReceived.ToList();
            var averageRating = reviewsReceived.Any()
                ? (decimal)reviewsReceived.Average(r => (decimal)r.Rating)
                : 0m;
            var totalReviews = reviewsReceived.Count;

            // Count total active offers
            var totalActiveOffers = user.Offers.Count;

            var response = new PublicUserProfileResponse
            {
                Id = user.Id,
                Name = user.Name,
                Description = user.Description,
                VerificationLevel = user.VerificationLevel,
                ReputationScore = user.ReputationScore,
                CreatedAt = user.CreatedAt,
                Skills = skillsWithCounts,
                Stats = new UserStatsDto
                {
                    AverageRating = Math.Round(averageRating, 2),
                    TotalReviews = totalReviews,
                    TotalActiveOffers = totalActiveOffers
                }
            };

            _logger.LogInformation("Retrieved public profile for user {UserId}", userId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving public profile for user {UserId}", userId);
            throw;
        }
    }
}
