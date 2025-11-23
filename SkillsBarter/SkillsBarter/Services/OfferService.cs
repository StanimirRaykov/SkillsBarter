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

            // Verify skill exists
            var skill = await _dbContext.Skills.FirstOrDefaultAsync(s => s.Id == request.SkillId);
            if (skill == null)
            {
                _logger.LogWarning("Create offer failed: Skill {SkillId} not found for user {UserId}", request.SkillId, userId);
                return null;
            }

            // Verify offer status exists
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
