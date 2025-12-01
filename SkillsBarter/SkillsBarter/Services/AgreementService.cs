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

    public async Task<AgreementResponse?> CreateAgreementAsync(Guid offerId, Guid requesterId, Guid providerId, string? terms)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var offer = await _dbContext.Offers
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == offerId);

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

            if (offer.UserId != requesterId && offer.UserId != providerId)
            {
                _logger.LogWarning("Create agreement failed: Neither requester {RequesterId} nor provider {ProviderId} owns offer {OfferId} (Owner: {OwnerId})",
                    requesterId, providerId, offerId, offer.UserId);
                return null;
            }

            var existingAgreement = await _dbContext.Agreements
                .Where(a => a.OfferId == offerId &&
                           (a.Status == AgreementStatus.Pending ||
                            a.Status == AgreementStatus.InProgress))
                .FirstOrDefaultAsync();

            if (existingAgreement != null)
            {
                _logger.LogWarning("Create agreement failed: Offer {OfferId} already has an active agreement {AgreementId}",
                    offerId, existingAgreement.Id);
                return null;
            }

            var agreement = new Agreement
            {
                Id = Guid.NewGuid(),
                OfferId = offerId,
                RequesterId = requesterId,
                ProviderId = providerId,
                Terms = terms,
                Status = AgreementStatus.InProgress,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Agreements.Add(agreement);

            offer.StatusCode = OfferStatusCode.UnderAgreement;
            offer.UpdatedAt = DateTime.UtcNow;
            _dbContext.Offers.Update(offer);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Agreement {AgreementId} created for offer {OfferId}. Offer status set to UnderAgreement",
                agreement.Id, offerId);

            return MapToAgreementResponse(agreement);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
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

            if (agreement.Status == AgreementStatus.Completed)
            {
                _logger.LogWarning("Complete agreement failed: Agreement {AgreementId} is already completed", agreementId);
                return null;
            }

            agreement.Status = AgreementStatus.Completed;
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

    public async Task<AgreementDetailResponse?> GetAgreementDetailByIdAsync(Guid agreementId)
    {
        try
        {
            var agreement = await _dbContext.Agreements
                .Include(a => a.Requester)
                .Include(a => a.Provider)
                .Include(a => a.Offer)
                    .ThenInclude(o => o.Skill)
                .Include(a => a.Milestones)
                .Include(a => a.Payments)
                .Include(a => a.Reviews)
                    .ThenInclude(r => r.Reviewer)
                .Include(a => a.Reviews)
                    .ThenInclude(r => r.Recipient)
                .FirstOrDefaultAsync(a => a.Id == agreementId);

            if (agreement == null)
            {
                _logger.LogWarning("Agreement {AgreementId} not found", agreementId);
                return null;
            }

            return MapToAgreementDetailResponse(agreement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agreement details {AgreementId}", agreementId);
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
            Terms = agreement.Terms,
            Status = agreement.Status,
            CreatedAt = agreement.CreatedAt,
            AcceptedAt = agreement.AcceptedAt,
            CompletedAt = agreement.CompletedAt
        };
    }

    private AgreementDetailResponse MapToAgreementDetailResponse(Agreement agreement)
    {
        return new AgreementDetailResponse
        {
            Id = agreement.Id,
            OfferId = agreement.OfferId,
            RequesterId = agreement.RequesterId,
            ProviderId = agreement.ProviderId,
            Terms = agreement.Terms,
            Status = agreement.Status,
            CreatedAt = agreement.CreatedAt,
            AcceptedAt = agreement.AcceptedAt,
            CompletedAt = agreement.CompletedAt,
            Requester = new AgreementUserInfo
            {
                Id = agreement.Requester.Id,
                Name = agreement.Requester.Name,
                Email = agreement.Requester.Email ?? string.Empty,
                VerificationLevel = agreement.Requester.VerificationLevel,
                ReputationScore = agreement.Requester.ReputationScore
            },
            Provider = new AgreementUserInfo
            {
                Id = agreement.Provider.Id,
                Name = agreement.Provider.Name,
                Email = agreement.Provider.Email ?? string.Empty,
                VerificationLevel = agreement.Provider.VerificationLevel,
                ReputationScore = agreement.Provider.ReputationScore
            },
            Offer = new AgreementOfferInfo
            {
                Id = agreement.Offer.Id,
                Title = agreement.Offer.Title,
                Description = agreement.Offer.Description,
                SkillName = agreement.Offer.Skill.Name,
                StatusCode = agreement.Offer.StatusCode.ToString()
            },
            Milestones = agreement.Milestones.Select(m => new MilestoneInfo
            {
                Id = m.Id,
                Title = m.Title,
                Amount = m.Amount,
                Status = m.Status,
                DueAt = m.DueAt
            }).ToList(),
            Payments = agreement.Payments.Select(p => new PaymentInfo
            {
                Id = p.Id,
                MilestoneId = p.MilestoneId,
                TipFromUserId = p.TipFromUserId,
                TipToUserId = p.TipToUserId,
                Amount = p.Amount,
                Currency = p.Currency,
                PaymentType = p.PaymentType,
                Status = p.Status,
                CreatedAt = p.CreatedAt
            }).ToList(),
            Reviews = agreement.Reviews.Select(r => new ReviewInfo
            {
                Id = r.Id,
                ReviewerId = r.ReviewerId,
                ReviewerName = r.Reviewer.Name,
                RecipientId = r.RecipientId,
                RecipientName = r.Recipient.Name,
                Rating = r.Rating,
                Body = r.Body,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }
}
