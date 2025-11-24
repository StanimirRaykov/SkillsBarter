using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class OfferService : IOfferService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<OfferService> _logger;
    private const string DefaultOfferStatus = "ACTIVE";

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

            var offerStatus = await _dbContext.OfferStatuses.FirstOrDefaultAsync(os => os.Code == DefaultOfferStatus);
            if (offerStatus == null)
            {
                _logger.LogError("Create offer failed: Default offer status '{Status}' not found", DefaultOfferStatus);
                return null;
            }

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SkillId = request.SkillId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                StatusCode = DefaultOfferStatus,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Offers.Add(offer);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Offer created successfully: {OfferId} by user {UserId}", offer.Id, userId);

            return MapToOfferResponse(offer, offerStatus.Label);
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
                .Where(o => o.StatusCode == "ACTIVE")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Skill))
            {
                query = query.Where(o => o.Skill.CategoryCode == request.Skill);
                query = query.Include(o => o.Skill);
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

            var offerResponses = offers.Select(o => MapToOfferResponse(o, o.Status?.Label ?? string.Empty)).ToList();

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

    private OfferResponse MapToOfferResponse(Offer offer, string statusLabel)
    {
        return new OfferResponse
        {
            Id = offer.Id,
            UserId = offer.UserId,
            SkillId = offer.SkillId,
            Title = offer.Title,
            Description = offer.Description,
            StatusCode = offer.StatusCode,
            StatusLabel = statusLabel,
            CreatedAt = offer.CreatedAt,
            UpdatedAt = offer.UpdatedAt
        };
    }
}
