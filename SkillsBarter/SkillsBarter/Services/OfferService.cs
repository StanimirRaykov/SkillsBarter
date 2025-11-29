using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class OfferService : IOfferService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<OfferService> _logger;

    public OfferService(ApplicationDbContext dbContext, ILogger<OfferService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OfferResponse?> CreateOfferAsync(Guid userId, CreateOfferRequest request)
    {
        try
        {
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

            var query = _dbContext.Offers
                .Include(o => o.Status)
                .Include(o => o.Skill)
                .Where(o => o.StatusCode == OfferStatusCode.Active)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Skill))
            {
                query = query.Where(o => o.Skill.CategoryCode == request.Skill);
            }

            if (request.SkillId.HasValue)
            {
                query = query.Where(o => o.SkillId == request.SkillId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var keyword = request.Q.ToLower();
                query = query.Where(o =>
                    o.Title.ToLower().Contains(keyword) ||
                    (o.Description != null && o.Description.ToLower().Contains(keyword))
                );
            }

            var total = await query.CountAsync();

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

    public async Task<OfferDetailResponse?> GetOfferByIdAsync(Guid offerId)
    {
        try
        {
            var offer = await _dbContext.Offers
                .Include(o => o.User)
                .Include(o => o.Status)
                .Include(o => o.Skill)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null)
            {
                _logger.LogWarning("Offer {OfferId} not found", offerId);
                return null;
            }

            if (offer.StatusCode != OfferStatusCode.Active)
            {
                _logger.LogInformation("Offer {OfferId} is not active (Status: {Status})", offerId, offer.StatusCode);
                return null;
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
    
    // For business purposes we do not delete offers, 
    // we mark them as cancelled
    // Might change in the future
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

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting offer {OfferId} for user {UserId}", offerId, userId);
            throw;
        }
    }

    private OfferResponse MapToOfferResponse(Offer offer)
    {
        return new OfferResponse
        {
            Id = offer.Id,
            UserId = offer.UserId,
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
