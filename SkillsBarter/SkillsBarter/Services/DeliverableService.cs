using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class DeliverableService : IDeliverableService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DeliverableService> _logger;

    public DeliverableService(ApplicationDbContext dbContext, ILogger<DeliverableService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DeliverableResponse?> SubmitDeliverableAsync(SubmitDeliverableRequest request, Guid userId)
    {
        var agreement = await _dbContext.Agreements
            .Include(a => a.Deliverables)
            .FirstOrDefaultAsync(a => a.Id == request.AgreementId);

        if (agreement == null)
        {
            _logger.LogWarning("Submit deliverable failed: Agreement {AgreementId} not found", request.AgreementId);
            return null;
        }

        if (!IsUserPartOfAgreement(agreement, userId))
        {
            _logger.LogWarning("Submit deliverable failed: User {UserId} is not part of agreement {AgreementId}",
                userId, request.AgreementId);
            return null;
        }

        if (agreement.Status != AgreementStatus.InProgress)
        {
            _logger.LogWarning("Submit deliverable failed: Agreement {AgreementId} is not in progress", request.AgreementId);
            return null;
        }

        var existingDeliverable = agreement.Deliverables.FirstOrDefault(d => d.SubmittedById == userId);
        if (existingDeliverable != null)
        {
            _logger.LogWarning("Submit deliverable failed: User {UserId} already submitted a deliverable for agreement {AgreementId}",
                userId, request.AgreementId);
            return null;
        }

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = request.AgreementId,
            SubmittedById = userId,
            Link = request.Link,
            Description = request.Description,
            Status = DeliverableStatus.Submitted,
            SubmittedAt = DateTime.UtcNow
        };

        _dbContext.Deliverables.Add(deliverable);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deliverable {DeliverableId} submitted by user {UserId} for agreement {AgreementId}",
            deliverable.Id, userId, request.AgreementId);

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<DeliverableResponse?> ApproveDeliverableAsync(Guid deliverableId, Guid userId)
    {
        var deliverable = await _dbContext.Deliverables
            .Include(d => d.Agreement)
            .Include(d => d.SubmittedBy)
            .FirstOrDefaultAsync(d => d.Id == deliverableId);

        if (deliverable == null)
        {
            _logger.LogWarning("Approve deliverable failed: Deliverable {DeliverableId} not found", deliverableId);
            return null;
        }

        if (!CanUserApprove(deliverable, userId))
        {
            _logger.LogWarning("Approve deliverable failed: User {UserId} cannot approve deliverable {DeliverableId}",
                userId, deliverableId);
            return null;
        }

        if (deliverable.Status != DeliverableStatus.Submitted)
        {
            _logger.LogWarning("Approve deliverable failed: Deliverable {DeliverableId} is not in submitted status", deliverableId);
            return null;
        }

        deliverable.Status = DeliverableStatus.Approved;
        deliverable.ApprovedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        await CheckAndCompleteAgreementAsync(deliverable.Agreement);

        _logger.LogInformation("Deliverable {DeliverableId} approved by user {UserId}", deliverableId, userId);

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<DeliverableResponse?> RequestRevisionAsync(Guid deliverableId, RequestRevisionRequest request, Guid userId)
    {
        var deliverable = await _dbContext.Deliverables
            .Include(d => d.Agreement)
            .Include(d => d.SubmittedBy)
            .FirstOrDefaultAsync(d => d.Id == deliverableId);

        if (deliverable == null)
        {
            _logger.LogWarning("Request revision failed: Deliverable {DeliverableId} not found", deliverableId);
            return null;
        }

        if (!CanUserApprove(deliverable, userId))
        {
            _logger.LogWarning("Request revision failed: User {UserId} cannot request revision for deliverable {DeliverableId}",
                userId, deliverableId);
            return null;
        }

        if (deliverable.Status != DeliverableStatus.Submitted)
        {
            _logger.LogWarning("Request revision failed: Deliverable {DeliverableId} is not in submitted status", deliverableId);
            return null;
        }

        deliverable.Status = DeliverableStatus.RevisionRequested;
        deliverable.RevisionReason = request.Reason;
        deliverable.RevisionCount++;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Revision requested for deliverable {DeliverableId} by user {UserId}", deliverableId, userId);

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<DeliverableResponse?> ResubmitDeliverableAsync(Guid deliverableId, SubmitDeliverableRequest request, Guid userId)
    {
        var deliverable = await _dbContext.Deliverables
            .Include(d => d.Agreement)
            .Include(d => d.SubmittedBy)
            .FirstOrDefaultAsync(d => d.Id == deliverableId);

        if (deliverable == null)
        {
            _logger.LogWarning("Resubmit deliverable failed: Deliverable {DeliverableId} not found", deliverableId);
            return null;
        }

        if (deliverable.SubmittedById != userId)
        {
            _logger.LogWarning("Resubmit deliverable failed: User {UserId} is not the submitter of deliverable {DeliverableId}",
                userId, deliverableId);
            return null;
        }

        if (deliverable.Status != DeliverableStatus.RevisionRequested)
        {
            _logger.LogWarning("Resubmit deliverable failed: Deliverable {DeliverableId} is not in revision requested status", deliverableId);
            return null;
        }

        deliverable.Link = request.Link;
        deliverable.Description = request.Description;
        deliverable.Status = DeliverableStatus.Submitted;
        deliverable.RevisionReason = null;
        deliverable.SubmittedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deliverable {DeliverableId} resubmitted by user {UserId}", deliverableId, userId);

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<DeliverableResponse?> GetDeliverableByIdAsync(Guid deliverableId, Guid userId)
    {
        var deliverable = await _dbContext.Deliverables
            .Include(d => d.Agreement)
            .Include(d => d.SubmittedBy)
            .FirstOrDefaultAsync(d => d.Id == deliverableId);

        if (deliverable == null || !IsUserPartOfAgreement(deliverable.Agreement, userId))
        {
            return null;
        }

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<AgreementDeliverablesResponse?> GetAgreementDeliverablesAsync(Guid agreementId, Guid userId)
    {
        var agreement = await _dbContext.Agreements
            .Include(a => a.Deliverables)
                .ThenInclude(d => d.SubmittedBy)
            .FirstOrDefaultAsync(a => a.Id == agreementId);

        if (agreement == null || !IsUserPartOfAgreement(agreement, userId))
        {
            return null;
        }

        var requesterDeliverable = agreement.Deliverables.FirstOrDefault(d => d.SubmittedById == agreement.RequesterId);
        var providerDeliverable = agreement.Deliverables.FirstOrDefault(d => d.SubmittedById == agreement.ProviderId);

        var bothApproved = requesterDeliverable?.Status == DeliverableStatus.Approved &&
                          providerDeliverable?.Status == DeliverableStatus.Approved;

        return new AgreementDeliverablesResponse
        {
            AgreementId = agreementId,
            RequesterDeliverable = requesterDeliverable != null ? await MapToResponseAsync(requesterDeliverable, userId) : null,
            ProviderDeliverable = providerDeliverable != null ? await MapToResponseAsync(providerDeliverable, userId) : null,
            BothApproved = bothApproved
        };
    }

    private async Task CheckAndCompleteAgreementAsync(Agreement agreement)
    {
        var deliverables = await _dbContext.Deliverables
            .Where(d => d.AgreementId == agreement.Id)
            .ToListAsync();

        var requesterDeliverable = deliverables.FirstOrDefault(d => d.SubmittedById == agreement.RequesterId);
        var providerDeliverable = deliverables.FirstOrDefault(d => d.SubmittedById == agreement.ProviderId);

        if (requesterDeliverable?.Status == DeliverableStatus.Approved &&
            providerDeliverable?.Status == DeliverableStatus.Approved)
        {
            agreement.Status = AgreementStatus.Completed;
            agreement.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Agreement {AgreementId} completed - both deliverables approved", agreement.Id);
        }
    }

    private static bool IsUserPartOfAgreement(Agreement agreement, Guid userId)
    {
        return agreement.RequesterId == userId || agreement.ProviderId == userId;
    }

    private static bool CanUserApprove(Deliverable deliverable, Guid userId)
    {
        var agreement = deliverable.Agreement;
        return IsUserPartOfAgreement(agreement, userId) && deliverable.SubmittedById != userId;
    }

    private async Task<DeliverableResponse> MapToResponseAsync(Deliverable deliverable, Guid currentUserId)
    {
        if (deliverable.SubmittedBy == null)
        {
            await _dbContext.Entry(deliverable).Reference(d => d.SubmittedBy).LoadAsync();
        }

        if (deliverable.Agreement == null)
        {
            await _dbContext.Entry(deliverable).Reference(d => d.Agreement).LoadAsync();
        }

        var canReview = deliverable.Status == DeliverableStatus.Submitted &&
                        deliverable.Agreement != null &&
                        IsUserPartOfAgreement(deliverable.Agreement, currentUserId) &&
                        deliverable.SubmittedById != currentUserId;

        return new DeliverableResponse
        {
            Id = deliverable.Id,
            AgreementId = deliverable.AgreementId,
            SubmittedById = deliverable.SubmittedById,
            SubmittedByName = deliverable.SubmittedBy?.Name ?? string.Empty,
            Link = deliverable.Link,
            Description = deliverable.Description,
            Status = deliverable.Status,
            RevisionReason = deliverable.RevisionReason,
            SubmittedAt = deliverable.SubmittedAt,
            ApprovedAt = deliverable.ApprovedAt,
            RevisionCount = deliverable.RevisionCount,
            CanApprove = canReview,
            CanRequestRevision = canReview
        };
    }
}
