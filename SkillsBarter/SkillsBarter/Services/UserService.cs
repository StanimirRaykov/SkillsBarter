using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }
    // Retrieves the public profile of a user by their ID
    public async Task<PublicUserProfileResponse?> GetPublicProfileAsync(Guid userId)
    {
        try
        {
            var user = await _dbContext.Users
                .Include(u => u.UserSkills)
                    .ThenInclude(us => us.Skill)
                        .ThenInclude(s => s.Category)
                .Include(u => u.Offers.Where(o => o.StatusCode == OfferStatusCode.Active))
                    .ThenInclude(o => o.Skill)
                .Include(u => u.ReviewsReceived)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("User not found with ID: {UserId}", userId);
                return null;
            }

            var directSkills = user.UserSkills
                .Select(us => new
                {
                    SkillId = us.Skill.Id,
                    SkillName = us.Skill.Name,
                    CategoryCode = us.Skill.CategoryCode
                })
                .ToList();

            var offerSkills = user.Offers
                .Select(o => new
                {
                    SkillId = o.Skill.Id,
                    SkillName = o.Skill.Name,
                    CategoryCode = o.Skill.CategoryCode
                })
                .ToList();

            var allSkillIds = directSkills.Select(s => s.SkillId)
                .Union(offerSkills.Select(s => s.SkillId))
                .Distinct();

            var combinedSkills = allSkillIds.Select(skillId =>
            {
                var skill = directSkills.FirstOrDefault(s => s.SkillId == skillId)
                         ?? offerSkills.First(s => s.SkillId == skillId);

                return new UserSkillDto
                {
                    SkillId = skill.SkillId,
                    SkillName = skill.SkillName,
                    CategoryCode = skill.CategoryCode,
                    ActiveOffersCount = user.Offers.Count(o => o.SkillId == skillId)
                };
            })
            .OrderByDescending(s => s.ActiveOffersCount)
            .ThenBy(s => s.SkillName)
            .ToList();

            var reviewsReceived = user.ReviewsReceived.ToList();
            var averageRating = reviewsReceived.Any()
                ? (decimal)reviewsReceived.Average(r => (decimal)r.Rating)
                : 0m;
            var totalReviews = reviewsReceived.Count;

            var totalActiveOffers = user.Offers.Count;

            var response = new PublicUserProfileResponse
            {
                Id = user.Id,
                Name = user.Name,
                Description = user.Description,
                VerificationLevel = user.VerificationLevel,
                ReputationScore = user.ReputationScore,
                CreatedAt = user.CreatedAt,
                Skills = combinedSkills,
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
    // Retrieves the full profile information of a user by their ID
    public async Task<DetailedUserProfileResponse?> GetDetailedProfileAsync(Guid userId)
    {
        try
        {
            var user = await _dbContext.Users
                .Include(u => u.UserSkills)
                    .ThenInclude(us => us.Skill)
                        .ThenInclude(s => s.Category)
                .Include(u => u.Offers.Where(o => o.StatusCode == OfferStatusCode.Active))
                    .ThenInclude(o => o.Skill)
                .Include(u => u.ReviewsReceived)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("User not found with ID: {UserId}", userId);
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);

            var directSkills = user.UserSkills
                .Select(us => new
                {
                    SkillId = us.Skill.Id,
                    SkillName = us.Skill.Name,
                    CategoryCode = us.Skill.CategoryCode
                })
                .ToList();

            var offerSkills = user.Offers
                .Select(o => new
                {
                    SkillId = o.Skill.Id,
                    SkillName = o.Skill.Name,
                    CategoryCode = o.Skill.CategoryCode
                })
                .ToList();

            var allSkillIds = directSkills.Select(s => s.SkillId)
                .Union(offerSkills.Select(s => s.SkillId))
                .Distinct();

            var combinedSkills = allSkillIds.Select(skillId =>
            {
                var skill = directSkills.FirstOrDefault(s => s.SkillId == skillId)
                         ?? offerSkills.First(s => s.SkillId == skillId);

                return new UserSkillDto
                {
                    SkillId = skill.SkillId,
                    SkillName = skill.SkillName,
                    CategoryCode = skill.CategoryCode,
                    ActiveOffersCount = user.Offers.Count(o => o.SkillId == skillId)
                };
            })
            .OrderByDescending(s => s.ActiveOffersCount)
            .ThenBy(s => s.SkillName)
            .ToList();

            // Calculating review statistics
            var reviewsReceived = user.ReviewsReceived.ToList();
            var averageRating = reviewsReceived.Any()
                ? (decimal)reviewsReceived.Average(r => (decimal)r.Rating)
                : 0m;
            var totalReviews = reviewsReceived.Count;
            var totalActiveOffers = user.Offers.Count;

            var response = new DetailedUserProfileResponse
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Name = user.Name,
                Description = user.Description,
                PhoneNumber = user.PhoneNumber,
                VerificationLevel = user.VerificationLevel,
                ReputationScore = user.ReputationScore,
                IsModerator = user.IsModerator,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Roles = roles.ToList(),
                Skills = combinedSkills,
                Stats = new UserStatsDto
                {
                    AverageRating = Math.Round(averageRating, 2),
                    TotalReviews = totalReviews,
                    TotalActiveOffers = totalActiveOffers
                }
            };

            _logger.LogInformation("Retrieved detailed profile for user {UserId}", userId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving detailed profile for user {UserId}", userId);
            throw;
        }
    }
}
