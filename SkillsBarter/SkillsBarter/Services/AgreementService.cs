using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class AgreementService : IAgreementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AgreementService> _logger;

    public AgreementService(ApplicationDbContext dbContext, ILogger<AgreementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AgreementResponse?> CreateAgreementAsync(Guid offerId, Guid requesterId, Guid providerId)
    {
        try
        {
            var offer = await _dbContext.Offers.FindAsync(offerId);
            if (offer == null)
            {
                _logger.LogWarning("Create agreement failed: Offer {OfferId} not found", offerId);
                return null;
            }

            if (offer.StatusCode != OfferStatusCode.Active)
            {
                _logger.LogWarning("Create agreement failed: Offer {OfferId} is not active (Status: {Status})",
                    offerId, offer.StatusCode);
                return null;
            }

            var requester = await _dbContext.Users.FindAsync(requesterId);
            if (requester == null)
            {
                _logger.LogWarning("Create agreement failed: Requester {RequesterId} not found", requesterId);
                return null;
            }

            var provider = await _dbContext.Users.FindAsync(providerId);
            if (provider == null)
            {
                _logger.LogWarning("Create agreement failed: Provider {ProviderId} not found", providerId);
                return null;
            }

            if (requesterId == providerId)
            {
                _logger.LogWarning("Create agreement failed: Requester and provider cannot be the same user");
                return null;
            }

            var agreement = new Agreement
            {
                Id = Guid.NewGuid(),
                OfferId = offerId,
                RequesterId = requesterId,
                ProviderId = providerId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Agreements.Add(agreement);

            offer.StatusCode = OfferStatusCode.UnderAgreement;
            offer.UpdatedAt = DateTime.UtcNow;
            _dbContext.Offers.Update(offer);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Agreement {AgreementId} created for offer {OfferId}. Offer status set to UnderAgreement",
                agreement.Id, offerId);

            return MapToAgreementResponse(agreement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agreement for offer {OfferId}", offerId);
            throw;
        }
    }

    public async Task<AgreementResponse?> CompleteAgreementAsync(Guid agreementId, Guid userId)
    {
        try
        {
            var agreement = await _dbContext.Agreements
                .Include(a => a.Offer)
                .FirstOrDefaultAsync(a => a.Id == agreementId);

            if (agreement == null)
            {
                _logger.LogWarning("Complete agreement failed: Agreement {AgreementId} not found", agreementId);
                return null;
            }

            if (agreement.RequesterId != userId && agreement.ProviderId != userId)
            {
                _logger.LogWarning("Complete agreement failed: User {UserId} is not part of agreement {AgreementId}",
                    userId, agreementId);
                return null;
            }

            if (agreement.Status == "Completed")
            {
                _logger.LogWarning("Complete agreement failed: Agreement {AgreementId} is already completed", agreementId);
                return null;
            }

            agreement.Status = "Completed";
            agreement.CompletedAt = DateTime.UtcNow;
            _dbContext.Agreements.Update(agreement);

            if (agreement.Offer != null)
            {
                agreement.Offer.StatusCode = OfferStatusCode.Completed;
                agreement.Offer.UpdatedAt = DateTime.UtcNow;
                _dbContext.Offers.Update(agreement.Offer);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Agreement {AgreementId} completed by user {UserId}. Offer {OfferId} status set to Completed",
                agreementId, userId, agreement.OfferId);

            return MapToAgreementResponse(agreement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing agreement {AgreementId}", agreementId);
            throw;
        }
    }

    public async Task<AgreementResponse?> GetAgreementByIdAsync(Guid agreementId)
    {
        try
        {
            var agreement = await _dbContext.Agreements
                .FirstOrDefaultAsync(a => a.Id == agreementId);

            if (agreement == null)
            {
                _logger.LogWarning("Agreement {AgreementId} not found", agreementId);
                return null;
            }

            return MapToAgreementResponse(agreement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agreement {AgreementId}", agreementId);
            throw;
        }
    }

    private AgreementResponse MapToAgreementResponse(Agreement agreement)
    {
        return new AgreementResponse
        {
            Id = agreement.Id,
            OfferId = agreement.OfferId,
            RequesterId = agreement.RequesterId,
            ProviderId = agreement.ProviderId,
            Status = agreement.Status,
            CreatedAt = agreement.CreatedAt,
            AcceptedAt = agreement.AcceptedAt,
            CompletedAt = agreement.CompletedAt
        };
    }
}
