using Microsoft.EntityFrameworkCore;
using SkillsBarter.Constants;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class AgreementService : IAgreementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AgreementService> _logger;

    public AgreementService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        ILogger<AgreementService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<AgreementResponse?> CreateAgreementAsync(
        Guid offerId,
        Guid requesterId,
        Guid providerId,
        string? terms,
        List<CreateMilestoneRequest>? milestones = null
    )
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            if (milestones == null || milestones.Count == 0)
            {
                _logger.LogWarning("Create agreement failed: At least one milestone is required");
                return null;
            }

            var offer = await _dbContext
                .Offers.Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null)
            {
                _logger.LogWarning("Create agreement failed: Offer {OfferId} not found", offerId);
                return null;
            }

            if (offer.StatusCode != OfferStatusCode.Active)
            {
                _logger.LogWarning(
                    "Create agreement failed: Offer {OfferId} is not active (Status: {Status})",
                    offerId,
                    offer.StatusCode
                );
                return null;
            }

            var requester = await _dbContext.Users.FindAsync(requesterId);
            if (requester == null)
            {
                _logger.LogWarning(
                    "Create agreement failed: Requester {RequesterId} not found",
                    requesterId
                );
                return null;
            }

            var provider = await _dbContext.Users.FindAsync(providerId);
            if (provider == null)
            {
                _logger.LogWarning(
                    "Create agreement failed: Provider {ProviderId} not found",
                    providerId
                );
                return null;
            }

            if (requesterId == providerId)
            {
                _logger.LogWarning(
                    "Create agreement failed: Requester and provider cannot be the same user"
                );
                return null;
            }

            if (offer.UserId != requesterId && offer.UserId != providerId)
            {
                _logger.LogWarning(
                    "Create agreement failed: Neither requester {RequesterId} nor provider {ProviderId} owns offer {OfferId} (Owner: {OwnerId})",
                    requesterId,
                    providerId,
                    offerId,
                    offer.UserId
                );
                return null;
            }

            var existingAgreement = await _dbContext
                .Agreements.Where(a =>
                    a.OfferId == offerId
                    && (
                        a.Status == AgreementStatus.Pending
                        || a.Status == AgreementStatus.InProgress
                    )
                )
                .FirstOrDefaultAsync();

            if (existingAgreement != null)
            {
                _logger.LogWarning(
                    "Create agreement failed: Offer {OfferId} already has an active agreement {AgreementId}",
                    offerId,
                    existingAgreement.Id
                );
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
                CreatedAt = DateTime.UtcNow,
            };

            _dbContext.Agreements.Add(agreement);

            offer.StatusCode = OfferStatusCode.UnderAgreement;
            offer.UpdatedAt = DateTime.UtcNow;
            _dbContext.Offers.Update(offer);

            if (milestones != null && milestones.Count > 0)
            {
                foreach (var milestoneRequest in milestones)
                {
                    var milestone = new Milestone
                    {
                        Id = Guid.NewGuid(),
                        AgreementId = agreement.Id,
                        Title = milestoneRequest.Title?.Trim() ?? string.Empty,
                        DurationInDays = milestoneRequest.DurationInDays,
                        Status = MilestoneStatus.Pending,
                        DueAt = milestoneRequest.DueAt
                    };
                    _dbContext.Milestones.Add(milestone);
                }
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Agreement {AgreementId} created for offer {OfferId}. Offer status set to UnderAgreement",
                agreement.Id,
                offerId
            );

            await _notificationService.CreateAsync(
                requesterId,
                NotificationType.AgreementCreated,
                "Agreement Created",
                $"Your offer was accepted by {provider.Name} for '{offer.Title}'"
            );
            await _notificationService.CreateAsync(
                providerId,
                NotificationType.AgreementCreated,
                "Agreement Created",
                $"You reached an agreement with {requester.Name} for '{offer.Title}'"
            );

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
            var agreement = await _dbContext
                .Agreements.Include(a => a.Offer)
                .FirstOrDefaultAsync(a => a.Id == agreementId);

            if (agreement == null)
            {
                _logger.LogWarning(
                    "Complete agreement failed: Agreement {AgreementId} not found",
                    agreementId
                );
                return null;
            }

            if (agreement.RequesterId != userId && agreement.ProviderId != userId)
            {
                _logger.LogWarning(
                    "Complete agreement failed: User {UserId} is not part of agreement {AgreementId}",
                    userId,
                    agreementId
                );
                return null;
            }

            if (agreement.Status == AgreementStatus.Completed)
            {
                _logger.LogWarning(
                    "Complete agreement failed: Agreement {AgreementId} is already completed",
                    agreementId
                );
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

            _logger.LogInformation(
                "Agreement {AgreementId} completed by user {UserId}. Offer {OfferId} status set to Completed",
                agreementId,
                userId,
                agreement.OfferId
            );

            await _notificationService.CreateAsync(
                agreement.RequesterId,
                NotificationType.AgreementCompleted,
                "Agreement Completed",
                $"Agreement #{agreementId.ToString()[..8]} is marked complete. Please leave a review."
            );
            await _notificationService.CreateAsync(
                agreement.ProviderId,
                NotificationType.AgreementCompleted,
                "Agreement Completed",
                $"Agreement #{agreementId.ToString()[..8]} is marked complete. Please leave a review."
            );

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
            var agreement = await _dbContext.Agreements.FirstOrDefaultAsync(a =>
                a.Id == agreementId
            );

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
            var agreement = await _dbContext
                .Agreements.Include(a => a.Requester)
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
            CompletedAt = agreement.CompletedAt,
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
                ReputationScore = agreement.Requester.ReputationScore,
            },
            Provider = new AgreementUserInfo
            {
                Id = agreement.Provider.Id,
                Name = agreement.Provider.Name,
                Email = agreement.Provider.Email ?? string.Empty,
                VerificationLevel = agreement.Provider.VerificationLevel,
                ReputationScore = agreement.Provider.ReputationScore,
            },
            Offer = new AgreementOfferInfo
            {
                Id = agreement.Offer.Id,
                Title = agreement.Offer.Title,
                Description = agreement.Offer.Description,
                SkillName = agreement.Offer.Skill.Name,
                StatusCode = agreement.Offer.StatusCode.ToString(),
            },
            Milestones = agreement
                .Milestones.Select(m => new MilestoneInfo
                {
                    Id = m.Id,
                    Title = m.Title,
                    DurationInDays = m.DurationInDays,
                    Status = m.Status,
                    DueAt = m.DueAt,
                })
                .ToList(),
            Payments = agreement
                .Payments.Select(p => new PaymentInfo
                {
                    Id = p.Id,
                    MilestoneId = p.MilestoneId,
                    TipFromUserId = p.TipFromUserId,
                    TipToUserId = p.TipToUserId,
                    Amount = p.Amount,
                    Currency = p.Currency,
                    PaymentType = p.PaymentType,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                })
                .ToList(),
            Reviews = agreement
                .Reviews.Select(r => new ReviewInfo
                {
                    Id = r.Id,
                    ReviewerId = r.ReviewerId,
                    ReviewerName = r.Reviewer.Name,
                    RecipientId = r.RecipientId,
                    RecipientName = r.Recipient.Name,
                    Rating = r.Rating,
                    Body = r.Body,
                    CreatedAt = r.CreatedAt,
                })
                .ToList(),
        };
    }

    public async Task ProcessAbandonedAgreementsAsync()
    {
        var abandonmentThreshold = DateTime.UtcNow.AddDays(-PenaltyConstants.AbandonmentDays);

        var abandonedAgreements = await _dbContext
            .Agreements.Include(a => a.Deliverables)
            .Where(a =>
                a.Status == AgreementStatus.InProgress
                && a.CreatedAt < abandonmentThreshold
                && !a.Deliverables.Any()
            )
            .ToListAsync();

        foreach (var agreement in abandonedAgreements)
        {
            CreatePenaltiesForAbandonedAgreement(agreement);
            agreement.Status = AgreementStatus.Cancelled;

            _logger.LogInformation(
                "Agreement {AgreementId} marked as abandoned. Penalties created for both parties.",
                agreement.Id
            );

            await _notificationService.CreateAsync(
                agreement.RequesterId,
                NotificationType.AgreementCancelled,
                "Agreement Cancelled",
                $"Agreement #{agreement.Id.ToString()[..8]} has been cancelled due to inactivity"
            );
            await _notificationService.CreateAsync(
                agreement.ProviderId,
                NotificationType.AgreementCancelled,
                "Agreement Cancelled",
                $"Agreement #{agreement.Id.ToString()[..8]} has been cancelled due to inactivity"
            );
        }

        if (abandonedAgreements.Count > 0)
            await _dbContext.SaveChangesAsync();
    }

    private void CreatePenaltiesForAbandonedAgreement(Agreement agreement)
    {
        var penalties = new[]
        {
            CreatePenalty(agreement.RequesterId, agreement.Id),
            CreatePenalty(agreement.ProviderId, agreement.Id),
        };

        _dbContext.Penalties.AddRange(penalties);
    }

    private static Penalty CreatePenalty(Guid userId, Guid agreementId) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AgreementId = agreementId,
            Amount = PenaltyConstants.FullPenaltyAmount,
            Currency = PenaltyConstants.DefaultCurrency,
            Reason = PenaltyReason.AgreementAbandoned,
            Status = PenaltyStatus.Charged,
            CreatedAt = DateTime.UtcNow,
            ChargedAt = DateTime.UtcNow,
        };
}
