using Microsoft.EntityFrameworkCore;
using SkillsBarter.Constants;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class DeliverableService : IDeliverableService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DeliverableService> _logger;

    public DeliverableService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        ILogger<DeliverableService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<DeliverableResponse?> SubmitDeliverableAsync(
        SubmitDeliverableRequest request,
        Guid userId
    )
    {
        var agreement = await _dbContext
            .Agreements.Include(a => a.Milestones)
                .ThenInclude(m => m.ResponsibleUser)
            .Include(a => a.Deliverables)
            .Include(a => a.Requester)
            .Include(a => a.Provider)
            .Include(a => a.Offer)
            .FirstOrDefaultAsync(a => a.Id == request.AgreementId);

        if (agreement == null)
        {
            var message = $"Agreement {request.AgreementId} not found.";
            _logger.LogWarning(message);
            throw new InvalidOperationException(message);
        }

        if (!IsUserPartOfAgreement(agreement, userId))
        {
            var message = "You are not part of this agreement.";
            _logger.LogWarning(
                "Submit deliverable failed: {Message} AgreementId={AgreementId}, UserId={UserId}",
                message,
                request.AgreementId,
                userId
            );
            throw new InvalidOperationException(message);
        }

        if (agreement.Status != AgreementStatus.InProgress)
        {
            var message = "Agreement is not in progress; deliverables cannot be submitted.";
            _logger.LogWarning(message + " AgreementId={AgreementId}", request.AgreementId);
            throw new InvalidOperationException(message);
        }

        var milestone = agreement.Milestones.FirstOrDefault(m => m.Id == request.MilestoneId);
        if (milestone == null)
        {
            var message = "Selected milestone does not exist for this agreement.";
            _logger.LogWarning(
                "{Message} MilestoneId={MilestoneId}, AgreementId={AgreementId}",
                message,
                request.MilestoneId,
                request.AgreementId
            );
            throw new InvalidOperationException(message);
        }

        if (milestone.ResponsibleUserId != userId)
        {
            var message = "You are not assigned to this milestone.";
            _logger.LogWarning(
                "{Message} UserId={UserId}, MilestoneId={MilestoneId}, AgreementId={AgreementId}",
                message,
                userId,
                request.MilestoneId,
                request.AgreementId
            );
            throw new InvalidOperationException(message);
        }

        var existingDeliverable = agreement.Deliverables.FirstOrDefault(d =>
            d.MilestoneId == request.MilestoneId
        );
        if (existingDeliverable != null)
        {
            var message = "A deliverable has already been submitted for this milestone.";
            _logger.LogWarning(
                "{Message} MilestoneId={MilestoneId}, AgreementId={AgreementId}",
                message,
                request.MilestoneId,
                request.AgreementId
            );
            throw new InvalidOperationException(message);
        }

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = request.AgreementId,
            SubmittedById = userId,
            MilestoneId = request.MilestoneId,
            Link = request.Link,
            Description = request.Description,
            Status = DeliverableStatus.Submitted,
            SubmittedAt = DateTime.UtcNow,
        };

        _dbContext.Deliverables.Add(deliverable);

        if (milestone.Status == MilestoneStatus.Pending)
        {
            milestone.Status = MilestoneStatus.InProgress;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Deliverable {DeliverableId} submitted by user {UserId} for agreement {AgreementId}",
            deliverable.Id,
            userId,
            request.AgreementId
        );

        var recipientId = userId == agreement.RequesterId ? agreement.ProviderId : agreement.RequesterId;
        var submitterName = userId == agreement.RequesterId ? agreement.Requester?.Name : agreement.Provider?.Name;
        await _notificationService.CreateAsync(
            recipientId,
            NotificationType.DeliverableSubmitted,
            "Deliverable Submitted",
            $"{submitterName ?? "A user"} submitted a deliverable for your agreement"
        );

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<DeliverableResponse?> ApproveDeliverableAsync(Guid deliverableId, Guid userId)
    {
        var deliverable = await _dbContext
            .Deliverables.Include(d => d.Agreement)
                .ThenInclude(a => a.Requester)
            .Include(d => d.Agreement)
                .ThenInclude(a => a.Provider)
            .Include(d => d.Agreement)
                .ThenInclude(a => a.Offer)
            .Include(d => d.SubmittedBy)
            .FirstOrDefaultAsync(d => d.Id == deliverableId);

        if (deliverable == null)
        {
            var message = "Deliverable not found.";
            _logger.LogWarning("{Message} DeliverableId={DeliverableId}", message, deliverableId);
            throw new InvalidOperationException(message);
        }

        if (!CanUserApprove(deliverable, userId))
        {
            var message = "You cannot approve your own deliverable.";
            _logger.LogWarning(
                "{Message} UserId={UserId}, DeliverableId={DeliverableId}",
                message,
                userId,
                deliverableId
            );
            throw new InvalidOperationException(message);
        }

        if (deliverable.Status != DeliverableStatus.Submitted)
        {
            var message = "Deliverable is not awaiting approval.";
            _logger.LogWarning("{Message} DeliverableId={DeliverableId}", message, deliverableId);
            throw new InvalidOperationException(message);
        }

        deliverable.Status = DeliverableStatus.Approved;
        deliverable.ApprovedAt = DateTime.UtcNow;

        if (deliverable.MilestoneId.HasValue)
        {
            var milestone = await _dbContext.Milestones.FindAsync(deliverable.MilestoneId.Value);
            if (milestone != null)
            {
                milestone.Status = MilestoneStatus.Completed;
            }
        }

        await _dbContext.SaveChangesAsync();
        await CheckAndCompleteAgreementAsync(deliverable.Agreement);

        _logger.LogInformation(
            "Deliverable {DeliverableId} approved by user {UserId}",
            deliverableId,
            userId
        );

        var approverName = userId == deliverable.Agreement.RequesterId
            ? deliverable.Agreement.Requester?.Name
            : deliverable.Agreement.Provider?.Name;
        await _notificationService.CreateAsync(
            deliverable.SubmittedById,
            NotificationType.DeliverableApproved,
            "Deliverable Approved",
            $"{approverName ?? "The other party"} approved your deliverable"
        );

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<DeliverableResponse?> RequestRevisionAsync(
        Guid deliverableId,
        RequestRevisionRequest request,
        Guid userId
    )
    {
        var deliverable = await _dbContext
            .Deliverables.Include(d => d.Agreement)
                .ThenInclude(a => a.Requester)
            .Include(d => d.Agreement)
                .ThenInclude(a => a.Provider)
            .Include(d => d.SubmittedBy)
            .FirstOrDefaultAsync(d => d.Id == deliverableId);

        if (deliverable == null)
        {
            var message = "Deliverable not found.";
            _logger.LogWarning("{Message} DeliverableId={DeliverableId}", message, deliverableId);
            throw new InvalidOperationException(message);
        }

        if (!CanUserApprove(deliverable, userId))
        {
            var message = "You cannot request a revision on your own deliverable.";
            _logger.LogWarning(
                "{Message} UserId={UserId}, DeliverableId={DeliverableId}",
                message,
                userId,
                deliverableId
            );
            throw new InvalidOperationException(message);
        }

        if (deliverable.Status != DeliverableStatus.Submitted)
        {
            var message = "Deliverable must be in submitted status to request a revision.";
            _logger.LogWarning("{Message} DeliverableId={DeliverableId}", message, deliverableId);
            throw new InvalidOperationException(message);
        }

        deliverable.Status = DeliverableStatus.RevisionRequested;
        deliverable.RevisionReason = request.Reason;
        deliverable.RevisionCount++;

        if (deliverable.MilestoneId.HasValue)
        {
            var milestone = await _dbContext.Milestones.FindAsync(deliverable.MilestoneId.Value);
            if (milestone != null)
            {
                milestone.Status = MilestoneStatus.InProgress;
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Revision requested for deliverable {DeliverableId} by user {UserId}",
            deliverableId,
            userId
        );

        var reviewerName = userId == deliverable.Agreement.RequesterId
            ? deliverable.Agreement.Requester?.Name
            : deliverable.Agreement.Provider?.Name;
        await _notificationService.CreateAsync(
            deliverable.SubmittedById,
            NotificationType.RevisionRequested,
            "Revision Requested",
            $"{reviewerName ?? "The other party"} requested a revision: {request.Reason}"
        );

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<DeliverableResponse?> ResubmitDeliverableAsync(
        Guid deliverableId,
        SubmitDeliverableRequest request,
        Guid userId
    )
    {
        var deliverable = await _dbContext
            .Deliverables.Include(d => d.Agreement)
                .ThenInclude(a => a.Requester)
            .Include(d => d.Agreement)
                .ThenInclude(a => a.Provider)
            .Include(d => d.SubmittedBy)
            .FirstOrDefaultAsync(d => d.Id == deliverableId);

        if (deliverable == null)
        {
            var message = "Deliverable not found.";
            _logger.LogWarning("{Message} DeliverableId={DeliverableId}", message, deliverableId);
            throw new InvalidOperationException(message);
        }

        if (request.MilestoneId != deliverable.MilestoneId || request.AgreementId != deliverable.AgreementId)
        {
            _logger.LogWarning(
                "Resubmit deliverable failed: Request data does not match deliverable {DeliverableId}",
                deliverableId
            );
            return null;
        }

        if (deliverable.SubmittedById != userId)
        {
            var message = "Only the original submitter can resubmit this deliverable.";
            _logger.LogWarning(
                "{Message} UserId={UserId}, DeliverableId={DeliverableId}",
                message,
                userId,
                deliverableId
            );
            throw new InvalidOperationException(message);
        }

        if (deliverable.Status != DeliverableStatus.RevisionRequested)
        {
            var message = "Deliverable must be in revision requested status to resubmit.";
            _logger.LogWarning("{Message} DeliverableId={DeliverableId}", message, deliverableId);
            throw new InvalidOperationException(message);
        }

        deliverable.Link = request.Link;
        deliverable.Description = request.Description;
        deliverable.Status = DeliverableStatus.Submitted;
        deliverable.RevisionReason = null;
        deliverable.SubmittedAt = DateTime.UtcNow;

        if (deliverable.MilestoneId.HasValue)
        {
            var milestone = await _dbContext.Milestones.FindAsync(deliverable.MilestoneId.Value);
            if (milestone != null && milestone.ResponsibleUserId == userId)
            {
                milestone.Status = MilestoneStatus.InProgress;
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Deliverable {DeliverableId} resubmitted by user {UserId}",
            deliverableId,
            userId
        );

        var recipientId = userId == deliverable.Agreement.RequesterId
            ? deliverable.Agreement.ProviderId
            : deliverable.Agreement.RequesterId;
        var submitterName = deliverable.SubmittedBy?.Name ?? "A user";
        await _notificationService.CreateAsync(
            recipientId,
            NotificationType.DeliverableSubmitted,
            "Deliverable Resubmitted",
            $"{submitterName} resubmitted a revised deliverable for your agreement"
        );

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<DeliverableResponse?> GetDeliverableByIdAsync(Guid deliverableId, Guid userId)
    {
        var deliverable = await _dbContext
            .Deliverables.Include(d => d.Agreement)
            .Include(d => d.SubmittedBy)
            .FirstOrDefaultAsync(d => d.Id == deliverableId);

        if (deliverable == null || !IsUserPartOfAgreement(deliverable.Agreement, userId))
        {
            return null;
        }

        return await MapToResponseAsync(deliverable, userId);
    }

    public async Task<AgreementDeliverablesResponse?> GetAgreementDeliverablesAsync(
        Guid agreementId,
        Guid userId
    )
    {
        var agreement = await _dbContext
            .Agreements.Include(a => a.Deliverables)
                .ThenInclude(d => d.SubmittedBy)
            .Include(a => a.Deliverables)
                .ThenInclude(d => d.Milestone)
            .Include(a => a.Milestones)
                .ThenInclude(m => m.ResponsibleUser)
            .FirstOrDefaultAsync(a => a.Id == agreementId);

        if (agreement == null || !IsUserPartOfAgreement(agreement, userId))
        {
            return null;
        }

        var milestoneResponses = new List<MilestoneDeliverableResponse>();

        foreach (var milestone in agreement.Milestones)
        {
            var deliverable = agreement.Deliverables.FirstOrDefault(d =>
                d.MilestoneId == milestone.Id
            );

            milestoneResponses.Add(new MilestoneDeliverableResponse
            {
                MilestoneId = milestone.Id,
                MilestoneTitle = milestone.Title,
                MilestoneStatus = milestone.Status,
                DurationInDays = milestone.DurationInDays,
                DueAt = milestone.DueAt,
                ResponsibleUserId = milestone.ResponsibleUserId,
                ResponsibleUserName = milestone.ResponsibleUser?.Name ?? string.Empty,
                Deliverable = deliverable != null
                    ? await MapToResponseAsync(deliverable, userId)
                    : null
            });
        }

        var allApproved = milestoneResponses.Count > 0
            && milestoneResponses.All(m => m.Deliverable?.Status == DeliverableStatus.Approved);

        return new AgreementDeliverablesResponse
        {
            AgreementId = agreementId,
            Milestones = milestoneResponses,
            AllApproved = allApproved,
        };
    }

    private async Task CheckAndCompleteAgreementAsync(Agreement agreement)
    {
        var milestones = await _dbContext
            .Milestones.Where(m => m.AgreementId == agreement.Id)
            .ToListAsync();

        if (milestones.Count == 0)
        {
            return;
        }

        var deliverables = await _dbContext
            .Deliverables.Where(d => d.AgreementId == agreement.Id)
            .ToListAsync();

        var allApproved = milestones.All(m =>
            deliverables.Any(d => d.MilestoneId == m.Id && d.Status == DeliverableStatus.Approved)
        );

        if (allApproved)
        {
            if (agreement.Offer == null)
            {
                await _dbContext.Entry(agreement).Reference(a => a.Offer).LoadAsync();
            }

            agreement.Status = AgreementStatus.Completed;
            agreement.CompletedAt = DateTime.UtcNow;
            if (agreement.Offer != null)
            {
                agreement.Offer.StatusCode = OfferStatusCode.Completed;
                agreement.Offer.UpdatedAt = DateTime.UtcNow;
                _dbContext.Offers.Update(agreement.Offer);
            }
            _dbContext.Agreements.Update(agreement);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Agreement {AgreementId} completed - all {MilestoneCount} milestone deliverables approved; offer marked Completed",
                agreement.Id,
                milestones.Count
            );
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

    private async Task<DeliverableResponse> MapToResponseAsync(
        Deliverable deliverable,
        Guid currentUserId
    )
    {
        if (deliverable.SubmittedBy == null)
        {
            await _dbContext.Entry(deliverable).Reference(d => d.SubmittedBy).LoadAsync();
        }

        if (deliverable.Agreement == null)
        {
            await _dbContext.Entry(deliverable).Reference(d => d.Agreement).LoadAsync();
        }

        if (deliverable.MilestoneId.HasValue && deliverable.Milestone == null)
        {
            await _dbContext.Entry(deliverable).Reference(d => d.Milestone).LoadAsync();
        }

        var canReview =
            deliverable.Status == DeliverableStatus.Submitted
            && deliverable.Agreement != null
            && IsUserPartOfAgreement(deliverable.Agreement, currentUserId)
            && deliverable.SubmittedById != currentUserId;

        return new DeliverableResponse
        {
            Id = deliverable.Id,
            AgreementId = deliverable.AgreementId,
            SubmittedById = deliverable.SubmittedById,
            SubmittedByName = deliverable.SubmittedBy?.Name ?? string.Empty,
            MilestoneId = deliverable.MilestoneId,
            MilestoneTitle = deliverable.Milestone?.Title,
            Link = deliverable.Link,
            Description = deliverable.Description,
            Status = deliverable.Status,
            RevisionReason = deliverable.RevisionReason,
            SubmittedAt = deliverable.SubmittedAt,
            ApprovedAt = deliverable.ApprovedAt,
            RevisionCount = deliverable.RevisionCount,
            CanApprove = canReview,
            CanRequestRevision = canReview,
        };
    }
}
