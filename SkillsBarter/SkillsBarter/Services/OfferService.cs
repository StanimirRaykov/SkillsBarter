using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkillsBarter.Constants;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class OfferService : IOfferService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OfferService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public OfferService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        ILogger<OfferService> logger,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
        _userManager = userManager;
    }

    public async Task<OfferResponse?> CreateOfferAsync(Guid userId, CreateOfferRequest request)
    {
        try
        {
            var (isAllowed, _, _) = await CheckOfferCreationCooldownAsync(userId);
            if (!isAllowed)
            {
                _logger.LogWarning("Offer creation denied for user {UserId} due to cooldown", userId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                _logger.LogWarning("Create offer failed: Empty title provided by user {UserId}", userId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                _logger.LogWarning("Create offer failed: Empty description provided by user {UserId}", userId);
                return null;
            }

            var skill = await _dbContext.Skills.FirstOrDefaultAsync(s => s.Id == request.SkillId);
            if (skill == null)
            {
                _logger.LogWarning("Create offer failed: Skill {SkillId} not found for user {UserId}", request.SkillId, userId);
                return null;
            }

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SkillId = request.SkillId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                StatusCode = OfferStatusCode.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Offers.Add(offer);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Offer created successfully: {OfferId} by user {UserId}", offer.Id, userId);

            return MapToOfferResponse(offer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating offer for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PaginatedResponse<OfferResponse>> GetOffersAsync(GetOffersRequest request)
    {
        try
        {
            request.Validate();

            _logger.LogInformation("OfferService.GetOffersAsync - Search Query: '{Q}', SkillId: {SkillId}, Skill: {Skill}, UserId: {UserId}",
                request.Q, request.SkillId, request.Skill, request.UserId);

            var query = _dbContext.Offers
                .Include(o => o.Status)
                .Include(o => o.Skill)
                .Include(o => o.User)
                .AsQueryable();

            if (request.UserId.HasValue)
            {
                query = query.Where(o =>
                    o.StatusCode == OfferStatusCode.Active ||
                    o.StatusCode == OfferStatusCode.UnderAgreement ||
                    o.StatusCode == OfferStatusCode.UnderReview ||
                    o.StatusCode == OfferStatusCode.Completed ||
                    o.StatusCode == OfferStatusCode.Cancelled);
            }
            else
            {
                query = query.Where(o => o.StatusCode == OfferStatusCode.Active);
            }

            if (!string.IsNullOrWhiteSpace(request.Skill))
            {
                _logger.LogInformation("Filtering by Skill category: {Skill}", request.Skill);
                query = query.Where(o => o.Skill.CategoryCode == request.Skill);
            }

            if (request.SkillId.HasValue)
            {
                _logger.LogInformation("Filtering by SkillId: {SkillId}", request.SkillId);
                query = query.Where(o => o.SkillId == request.SkillId.Value);
            }

            if (request.UserId.HasValue)
            {
                _logger.LogInformation("Filtering by UserId: {UserId}", request.UserId);
                query = query.Where(o => o.UserId == request.UserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var keyword = request.Q.ToLower();
                _logger.LogInformation("Applying search filter with keyword: '{Keyword}'", keyword);
                query = query.Where(o =>
                    o.Title.ToLower().Contains(keyword) ||
                    (o.Description != null && o.Description.ToLower().Contains(keyword))
                );
            }

            var total = await query.CountAsync();
            _logger.LogInformation("🔎 Found {Total} offers matching criteria", total);

            var offers = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var offerResponses = offers.Select(o => MapToOfferResponse(o)).ToList();

            return new PaginatedResponse<OfferResponse>
            {
                Items = offerResponses,
                Page = request.Page,
                PageSize = request.PageSize,
                Total = total
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving offers with filters: {@Request}", request);
            throw;
        }
    }
    public async Task<PaginatedResponse<OfferResponse>> GetMyOffersAsync(Guid userId, GetOffersRequest request)
    {
        try
        {
            request.Validate();

            _logger.LogInformation("OfferService.GetMyOffersAsync - User: {UserId}, Search Query: '{Q}', SkillId: {SkillId}, Skill: {Skill}",
                userId, request.Q, request.SkillId, request.Skill);

            var query = _dbContext.Offers
                .Include(o => o.Status)
                .Include(o => o.Skill)
                .Include(o => o.User)
                .Where(o => o.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Skill))
            {
                _logger.LogInformation("Filtering by Skill category: {Skill}", request.Skill);
                query = query.Where(o => o.Skill.CategoryCode == request.Skill);
            }

            if (request.SkillId.HasValue)
            {
                _logger.LogInformation("Filtering by SkillId: {SkillId}", request.SkillId);
                query = query.Where(o => o.SkillId == request.SkillId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var keyword = request.Q.ToLower();
                _logger.LogInformation("Applying search filter with keyword: '{Keyword}'", keyword);
                query = query.Where(o =>
                    o.Title.ToLower().Contains(keyword) ||
                    (o.Description != null && o.Description.ToLower().Contains(keyword))
                );
            }

            var total = await query.CountAsync();
            _logger.LogInformation("Found {Total} offers for user {UserId}", total, userId);

            var offers = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var offerResponses = offers.Select(o => MapToOfferResponse(o)).ToList();

            return new PaginatedResponse<OfferResponse>
            {
                Items = offerResponses,
                Page = request.Page,
                PageSize = request.PageSize,
                Total = total
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving offers for user {UserId} with filters: {@Request}", userId, request);
            throw;
        }
    }

    public async Task<OfferDetailResponse?> GetOfferByIdAsync(Guid offerId, Guid? userId = null)
    {
        try
        {
            var offer = await _dbContext.Offers
                .Include(o => o.User)
                .Include(o => o.Status)
                .Include(o => o.Skill)
                .Include(o => o.Agreements)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null)
            {
                _logger.LogWarning("Offer {OfferId} not found", offerId);
                return null;
            }

            if (offer.StatusCode != OfferStatusCode.Active)
            {
                var canView = false;

                if (offer.StatusCode == OfferStatusCode.Completed)
                {
                    canView = true;
                }
                else if (userId.HasValue)
                {
                    if (offer.UserId == userId.Value)
                    {
                        canView = true;
                    }
                    else
                    {
                        var isParticipant = offer.Agreements.Any(a =>
                            (a.RequesterId == userId.Value || a.ProviderId == userId.Value) &&
                            (a.Status == AgreementStatus.Completed));

                        if (isParticipant)
                        {
                            canView = true;
                        }
                    }
                }

                if (!canView)
                {
                    _logger.LogInformation("Offer {OfferId} is not active (Status: {Status}) and user {UserId} is not authorized to view it", offerId, offer.StatusCode, userId);
                    return null;
                }
            }

            if (offer.User == null)
            {
                _logger.LogError("Offer {OfferId} has no associated user", offerId);
                return null;
            }

            if (offer.Skill == null)
            {
                _logger.LogError("Offer {OfferId} has no associated skill", offerId);
                return null;
            }

            var averageRating = await _dbContext.Reviews
                .Where(r => r.RecipientId == offer.UserId)
                .AverageAsync(r => (decimal?)r.Rating) ?? 0m;

            return new OfferDetailResponse
            {
                Id = offer.Id,
                UserId = offer.UserId,
                SkillId = offer.SkillId,
                SkillName = offer.Skill.Name,
                SkillCategoryCode = offer.Skill.CategoryCode,
                Title = offer.Title,
                Description = offer.Description,
                StatusCode = offer.StatusCode.ToString(),
                StatusLabel = offer.Status?.Label ?? offer.StatusCode.ToString(),
                CreatedAt = offer.CreatedAt,
                UpdatedAt = offer.UpdatedAt,
                Owner = new OfferOwnerInfo
                {
                    Id = offer.User.Id,
                    Name = offer.User.Name,
                    Rating = averageRating
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving offer {OfferId}", offerId);
            throw;
        }
    }

    public async Task<OfferResponse?> UpdateOfferAsync(Guid offerId, Guid userId, UpdateOfferRequest request, bool isAdmin)
    {
        try
        {
            var offer = await _dbContext.Offers
                .Include(o => o.Status)
                .Include(o => o.Skill)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null)
            {
                _logger.LogWarning("Update offer failed: Offer {OfferId} not found", offerId);
                return null;
            }

            if (offer.UserId != userId && !isAdmin)
            {
                _logger.LogWarning("Update offer failed: User {UserId} is not authorized to update offer {OfferId}", userId, offerId);
                return null;
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                offer.Title = request.Title.Trim();
            }

            if (request.Description != null)
            {
                offer.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            }

            if (request.SkillId.HasValue)
            {
                var skill = await _dbContext.Skills.FirstOrDefaultAsync(s => s.Id == request.SkillId.Value);
                if (skill == null)
                {
                    _logger.LogWarning("Update offer failed: Skill {SkillId} not found", request.SkillId.Value);
                    return null;
                }
                offer.SkillId = request.SkillId.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.StatusCode))
            {
                if (Enum.TryParse<OfferStatusCode>(request.StatusCode, true, out var statusCode))
                {
                    offer.StatusCode = statusCode;
                }
                else
                {
                    _logger.LogWarning("Update offer failed: Invalid status code {StatusCode}", request.StatusCode);
                    return null;
                }
            }

            offer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Offer {OfferId} updated successfully by user {UserId}", offerId, userId);

            return MapToOfferResponse(offer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating offer {OfferId} for user {UserId}", offerId, userId);
            throw;
        }
    }
    public async Task<bool> DeleteOfferAsync(Guid offerId, Guid userId, bool isAdmin)
    {
        try
        {
            var offer = await _dbContext.Offers.FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null)
            {
                _logger.LogWarning("Delete offer failed: Offer {OfferId} not found", offerId);
                return false;
            }

            // Only owner or admin can delete
            if (offer.UserId != userId && !isAdmin)
            {
                _logger.LogWarning("Delete offer failed: User {UserId} is not authorized to delete offer {OfferId}", userId, offerId);
                return false;
            }

            offer.StatusCode = OfferStatusCode.Cancelled;
            offer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Offer {OfferId} marked as cancelled by user {UserId}", offerId, userId);

            if (isAdmin && offer.UserId != userId)
            {
                await _notificationService.CreateAsync(
                    offer.UserId,
                    NotificationType.OfferDeactivated,
                    "Offer Deactivated",
                    $"Your offer '{offer.Title}' has been deactivated by an administrator"
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting offer {OfferId} for user {UserId}", offerId, userId);
            throw;
        }
    }

    public async Task<(bool IsAllowed, string? ErrorMessage)> CheckOfferCreationAllowedAsync(Guid userId)
    {
        var (isAllowed, _, remainingCooldown) = await CheckOfferCreationCooldownAsync(userId);

        if (!isAllowed)
        {
            if (remainingCooldown == null)
            {
                return (false, "You already have an active offer. Freemium users can only have one offer at a time. Please wait for your current offer to be completed or cancelled before creating a new one. Upgrade to Premium for unlimited offers.");
            }

            var days = remainingCooldown.Value.Days;
            var hours = remainingCooldown.Value.Hours;
            var message = days > 0
                ? $"You must wait {days} day(s) and {hours} hour(s) before creating another offer. Freemium users can create one new offer per week after their previous offer is completed. Upgrade to Premium for unlimited offers."
                : $"You must wait {hours} hour(s) before creating another offer. Freemium users can create one new offer per week after their previous offer is completed. Upgrade to Premium for unlimited offers.";
            return (false, message);
        }

        return (true, null);
    }

    private async Task<(bool IsAllowed, DateTime? LastOfferCompletedAt, TimeSpan? RemainingCooldown)> CheckOfferCreationCooldownAsync(Guid userId)
    {
        const int COOLDOWN_DAYS = 7;

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for cooldown check", userId);
            return (false, null, null);
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(AppRoles.Premium) ||
            roles.Contains(AppRoles.Admin) ||
            roles.Contains(AppRoles.Moderator))
        {
            _logger.LogInformation("User {UserId} with role(s) {Roles} bypasses offer creation restrictions",
                userId, string.Join(", ", roles));
            return (true, null, null);
        }

        var hasActiveOffer = await _dbContext.Offers
            .AnyAsync(o => o.UserId == userId &&
                          (o.StatusCode == OfferStatusCode.Active ||
                           o.StatusCode == OfferStatusCode.UnderAgreement ||
                           o.StatusCode == OfferStatusCode.UnderReview));

        if (hasActiveOffer)
        {
            _logger.LogWarning("User {UserId} attempted to create offer but already has an active offer. Freemium users can only have one offer at a time", userId);
            return (false, null, null);
        }

        var lastCompletedOffer = await _dbContext.Offers
            .Where(o => o.UserId == userId && o.StatusCode == OfferStatusCode.Completed)
            .OrderByDescending(o => o.UpdatedAt)
            .FirstOrDefaultAsync();

        if (lastCompletedOffer == null)
        {
            _logger.LogInformation("User {UserId} has no completed offers, allowing offer creation", userId);
            return (true, null, null);
        }

        var timeSinceLastCompletion = DateTime.UtcNow - lastCompletedOffer.UpdatedAt;
        var cooldownPeriod = TimeSpan.FromDays(COOLDOWN_DAYS);

        if (timeSinceLastCompletion < cooldownPeriod)
        {
            var remainingCooldown = cooldownPeriod - timeSinceLastCompletion;
            _logger.LogWarning(
                "User {UserId} attempted to create offer during cooldown. Last offer completed: {LastCompletionDate}, Remaining cooldown: {RemainingDays} days, {RemainingHours} hours",
                userId, lastCompletedOffer.UpdatedAt, remainingCooldown.Days, remainingCooldown.Hours);
            return (false, lastCompletedOffer.UpdatedAt, remainingCooldown);
        }

        _logger.LogInformation("User {UserId} passed cooldown check. Last offer completed {Days} days ago",
            userId, timeSinceLastCompletion.Days);
        return (true, lastCompletedOffer.UpdatedAt, null);
    }

    private OfferResponse MapToOfferResponse(Offer offer)
    {
        return new OfferResponse
        {
            Id = offer.Id,
            UserId = offer.UserId,
            OwnerName = offer.User?.Name ?? "Unknown User",
            SkillId = offer.SkillId,
            Title = offer.Title,
            Description = offer.Description,
            StatusCode = offer.StatusCode.ToString(),
            StatusLabel = offer.Status?.Label ?? offer.StatusCode.ToString(),
            CreatedAt = offer.CreatedAt,
            UpdatedAt = offer.UpdatedAt
        };
    }
}
