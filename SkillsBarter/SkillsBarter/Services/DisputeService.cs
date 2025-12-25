using Microsoft.EntityFrameworkCore;
using SkillsBarter.Constants;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class DisputeService : IDisputeService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DisputeService> _logger;
    private const int ResponseDeadlineHours = 72;

    public DisputeService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        ILogger<DisputeService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<DisputeResponse?> OpenDisputeAsync(OpenDisputeRequest request, Guid userId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var agreement = await _dbContext.Agreements
                .Include(a => a.Deliverables)
                    .ThenInclude(d => d.Milestone)
                .Include(a => a.Requester)
                .Include(a => a.Provider)
                .FirstOrDefaultAsync(a => a.Id == request.AgreementId);

            if (agreement == null)
            {
                _logger.LogWarning("Open dispute failed: Agreement {AgreementId} not found", request.AgreementId);
                return null;
            }

            if (!IsUserPartOfAgreement(agreement, userId))
            {
                _logger.LogWarning("Open dispute failed: User {UserId} not part of agreement {AgreementId}",
                    userId, request.AgreementId);
                return null;
            }

            if (agreement.Status != AgreementStatus.InProgress)
            {
                _logger.LogWarning("Open dispute failed: Agreement {AgreementId} is not in progress", request.AgreementId);
                return null;
            }

            var existingDispute = await _dbContext.Disputes
                .AnyAsync(d => d.AgreementId == request.AgreementId &&
                              d.Status != DisputeStatus.Closed &&
                              d.Status != DisputeStatus.Resolved);

            if (existingDispute)
            {
                _logger.LogWarning("Open dispute failed: Active dispute exists for agreement {AgreementId}",
                    request.AgreementId);
                return null;
            }

            var respondentId = agreement.RequesterId == userId ? agreement.ProviderId : agreement.RequesterId;

            var proposal = await _dbContext.Proposals
                .FirstOrDefaultAsync(p => p.AgreementId == agreement.Id);

            var deadline = proposal?.Deadline ?? DateTime.UtcNow.AddDays(7);

            var scoreData = CalculateScoreData(agreement, userId, respondentId, deadline);

            var systemDecision = GetSystemDecisionFromScore(scoreData.Score);

            var dispute = new Dispute
            {
                Id = Guid.NewGuid(),
                AgreementId = request.AgreementId,
                OpenedById = userId,
                RespondentId = respondentId,
                ReasonCode = request.ReasonCode,
                Description = request.Description,
                Status = DisputeStatus.AwaitingResponse,
                SystemDecision = systemDecision,
                Resolution = systemDecision == DisputeSystemDecision.EscalateToModerator
                    ? DisputeResolution.None
                    : GetResolutionForDecision(systemDecision),
                Score = scoreData.Score,
                ComplainerDelivered = scoreData.ComplainerDelivered,
                RespondentDelivered = scoreData.RespondentDelivered,
                ComplainerOnTime = scoreData.ComplainerOnTime,
                RespondentOnTime = scoreData.RespondentOnTime,
                ComplainerApprovedBeforeDispute = scoreData.ComplainerApprovedBeforeDispute,
                RespondentApprovedBeforeDispute = scoreData.RespondentApprovedBeforeDispute,
                CreatedAt = DateTime.UtcNow,
                ResponseDeadline = DateTime.UtcNow.AddHours(ResponseDeadlineHours)
            };

            _dbContext.Disputes.Add(dispute);

            foreach (var evidence in request.Evidence)
            {
                var evidenceEntity = new DisputeEvidence
                {
                    Id = Guid.NewGuid(),
                    DisputeId = dispute.Id,
                    SubmittedById = userId,
                    Link = evidence.Link,
                    Description = evidence.Description,
                    SubmittedAt = DateTime.UtcNow
                };
                _dbContext.Set<DisputeEvidence>().Add(evidenceEntity);
            }

            agreement.Status = AgreementStatus.Disputed;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Dispute {DisputeId} opened by {UserId} for agreement {AgreementId} with score {Score}",
                dispute.Id, userId, request.AgreementId, dispute.Score);

            var complainerName = agreement.RequesterId == userId ? agreement.Requester.Name : agreement.Provider.Name;
            await _notificationService.CreateAsync(
                respondentId,
                NotificationType.DisputeOpened,
                "Dispute Opened",
                $"{complainerName} opened a dispute against you. Respond within 72 hours."
            );

            return await MapToResponseAsync(dispute, userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error opening dispute for agreement {AgreementId} by user {UserId}", request.AgreementId, userId);
            throw;
        }
    }

    public async Task<DisputeResponse?> RespondToDisputeAsync(Guid disputeId, RespondToDisputeRequest request, Guid userId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var dispute = await GetDisputeWithIncludesAsync(disputeId);

            if (dispute == null)
            {
                _logger.LogWarning("Respond to dispute failed: Dispute {DisputeId} not found", disputeId);
                return null;
            }

            if (await EnforceResponseDeadlineAsync(dispute))
            {
                await transaction.CommitAsync();
                return await MapToResponseAsync(dispute, userId);
            }

            if (dispute.RespondentId != userId)
            {
                _logger.LogWarning("Respond to dispute failed: User {UserId} is not respondent of {DisputeId}",
                    userId, disputeId);
                return null;
            }

            if (dispute.Status != DisputeStatus.AwaitingResponse)
            {
                _logger.LogWarning("Respond to dispute failed: Dispute {DisputeId} not awaiting response", disputeId);
                return null;
            }

            var message = new DisputeMessage
            {
                Id = Guid.NewGuid(),
                DisputeId = disputeId,
                SenderId = userId,
                Body = request.Response,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.DisputeMessages.Add(message);

            foreach (var evidence in request.Evidence)
            {
                var evidenceEntity = new DisputeEvidence
                {
                    Id = Guid.NewGuid(),
                    DisputeId = disputeId,
                    SubmittedById = userId,
                    Link = evidence.Link,
                    Description = evidence.Description,
                    SubmittedAt = DateTime.UtcNow
                };
                _dbContext.Set<DisputeEvidence>().Add(evidenceEntity);
            }

            dispute.ResponseReceivedAt = DateTime.UtcNow;
            dispute.Status = DisputeStatus.UnderReview;

            ResolveOrEscalate(dispute);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Dispute {DisputeId} responded to by {UserId}", disputeId, userId);

            await _notificationService.CreateAsync(
                dispute.OpenedById,
                NotificationType.DisputeResponse,
                "Dispute Response Received",
                $"{dispute.Respondent?.Name ?? "The respondent"} replied to your dispute"
            );

            if (dispute.Status == DisputeStatus.EscalatedToModerator)
            {
                await _notificationService.CreateAsync(
                    dispute.OpenedById,
                    NotificationType.DisputeEscalated,
                    "Dispute Escalated",
                    "Your dispute has been escalated to a moderator for review"
                );
                await _notificationService.CreateAsync(
                    dispute.RespondentId,
                    NotificationType.DisputeEscalated,
                    "Dispute Escalated",
                    "The dispute has been escalated to a moderator for review"
                );
            }
            else if (dispute.Status == DisputeStatus.UnderReview)
            {
                await _notificationService.CreateAsync(
                    dispute.OpenedById,
                    NotificationType.DisputeResponse,
                    "Decision Ready",
                    "A system decision is available. Accept it or escalate to moderator."
                );
                await _notificationService.CreateAsync(
                    dispute.RespondentId,
                    NotificationType.DisputeResponse,
                    "Decision Ready",
                    "A system decision is available. Accept it or escalate to moderator."
                );
            }
            else if (dispute.Status == DisputeStatus.Resolved)
            {
                await NotifyDisputeResolutionAsync(dispute);
            }

            return await MapToResponseAsync(dispute, userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error responding to dispute {DisputeId} by user {UserId}", disputeId, userId);
            throw;
        }
    }

    public async Task<DisputeResponse?> AcceptSystemDecisionAsync(Guid disputeId, AcceptDecisionRequest request, Guid userId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var dispute = await GetDisputeWithIncludesAsync(disputeId);

            if (dispute == null)
            {
                _logger.LogWarning("Accept decision failed: Dispute {DisputeId} not found", disputeId);
                return null;
            }

            if (!IsUserPartOfDispute(dispute, userId))
            {
                _logger.LogWarning("Accept decision failed: User {UserId} not part of dispute {DisputeId}", userId, disputeId);
                return null;
            }

            if (await EnforceResponseDeadlineAsync(dispute))
            {
                await transaction.CommitAsync();
                return await MapToResponseAsync(dispute, userId);
            }

            if (dispute.Status != DisputeStatus.UnderReview || dispute.SystemDecision == DisputeSystemDecision.EscalateToModerator)
            {
                _logger.LogWarning("Accept decision failed: Dispute {DisputeId} not awaiting acceptance", disputeId);
                return null;
            }

            if (request.Accept)
            {
                if (dispute.OpenedById == userId)
                    dispute.ComplainerDecision = DisputePartyDecision.Accept;
                else if (dispute.RespondentId == userId)
                    dispute.RespondentDecision = DisputePartyDecision.Accept;
            }
            else
            {
                if (dispute.OpenedById == userId)
                    dispute.ComplainerDecision = DisputePartyDecision.Reject;
                else if (dispute.RespondentId == userId)
                    dispute.RespondentDecision = DisputePartyDecision.Reject;

                SetEscalatedStatus(dispute, "Participant rejected the system decision");
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                await NotifyEscalationAsync(dispute);

                return await MapToResponseAsync(dispute, userId);
            }

            await _dbContext.SaveChangesAsync();

            if (dispute.ComplainerDecision == DisputePartyDecision.Accept &&
                dispute.RespondentDecision == DisputePartyDecision.Accept)
            {
                await FinalizeSystemDecisionAsync(dispute);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("User {UserId} recorded decision for dispute {DisputeId}", userId, disputeId);

            return await MapToResponseAsync(dispute, userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error accepting decision for dispute {DisputeId} by user {UserId}", disputeId, userId);
            throw;
        }
    }

    public async Task<DisputeResponse?> EscalateDisputeAsync(Guid disputeId, EscalateDisputeRequest request, Guid userId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var dispute = await GetDisputeWithIncludesAsync(disputeId);

            if (dispute == null)
            {
                _logger.LogWarning("Escalate dispute failed: Dispute {DisputeId} not found", disputeId);
                return null;
            }

            if (!IsUserPartOfDispute(dispute, userId))
            {
                _logger.LogWarning("Escalate dispute failed: User {UserId} not part of dispute {DisputeId}", userId, disputeId);
                return null;
            }

            if (await EnforceResponseDeadlineAsync(dispute))
            {
                await transaction.CommitAsync();
                return await MapToResponseAsync(dispute, userId);
            }

            if (dispute.Status == DisputeStatus.Resolved || dispute.Status == DisputeStatus.Closed)
            {
                _logger.LogWarning("Escalate dispute failed: Dispute {DisputeId} already closed", disputeId);
                return null;
            }

            if (dispute.OpenedById == userId)
                dispute.ComplainerDecision = DisputePartyDecision.Reject;
            else if (dispute.RespondentId == userId)
                dispute.RespondentDecision = DisputePartyDecision.Reject;

            SetEscalatedStatus(dispute, request.Reason);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await NotifyEscalationAsync(dispute);

            _logger.LogInformation("Dispute {DisputeId} escalated to moderator by {UserId}", disputeId, userId);

            return await MapToResponseAsync(dispute, userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error escalating dispute {DisputeId} by user {UserId}", disputeId, userId);
            throw;
        }
    }

    public async Task<DisputeResponse?> AddEvidenceAsync(Guid disputeId, EvidenceRequest request, Guid userId)
    {
        var dispute = await GetDisputeWithIncludesAsync(disputeId);

        if (dispute == null)
        {
            _logger.LogWarning("Add evidence failed: Dispute {DisputeId} not found", disputeId);
            return null;
        }

        if (!CanUserAddEvidence(dispute, userId))
        {
            _logger.LogWarning("Add evidence failed: User {UserId} cannot add to dispute {DisputeId}",
                userId, disputeId);
            return null;
        }

        var evidence = new DisputeEvidence
        {
            Id = Guid.NewGuid(),
            DisputeId = disputeId,
            SubmittedById = userId,
            Link = request.Link,
            Description = request.Description,
            SubmittedAt = DateTime.UtcNow
        };

        _dbContext.Set<DisputeEvidence>().Add(evidence);
        await _dbContext.SaveChangesAsync();

        await _dbContext.Entry(dispute).Collection(d => d.Evidence).LoadAsync();

        _logger.LogInformation("Evidence added to dispute {DisputeId} by {UserId}", disputeId, userId);

        return await MapToResponseAsync(dispute, userId);
    }

    public async Task<DisputeResponse?> GetDisputeByIdAsync(Guid disputeId, Guid userId)
    {
        var dispute = await GetDisputeWithIncludesAsync(disputeId);

        if (dispute == null)
            return null;

        var isModerator = await _dbContext.Users.AnyAsync(u => u.Id == userId && u.IsModerator);
        if (!IsUserPartOfDispute(dispute, userId) && !isModerator)
            return null;

        await EnforceResponseDeadlineAsync(dispute);

        return await MapToResponseAsync(dispute, userId);
    }

    public async Task<List<DisputeListResponse>> GetMyDisputesAsync(Guid userId)
    {
        await ProcessExpiredDisputesAsync();

        var disputes = await _dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.OpenedBy)
            .Include(d => d.Respondent)
            .Where(d => d.OpenedById == userId || d.RespondentId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return disputes.Select(d => MapToListResponse(d, userId, false)).ToList();
    }

    public async Task<List<DisputeListResponse>> GetDisputesForModerationAsync(Guid moderatorId)
    {
        var isModerator = await _dbContext.Users.AsNoTracking().AnyAsync(u => u.Id == moderatorId && u.IsModerator);
        if (!isModerator)
            return new List<DisputeListResponse>();

        await ProcessExpiredDisputesAsync();

        var disputes = await _dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.OpenedBy)
            .Include(d => d.Respondent)
            .Where(d => d.Status == DisputeStatus.EscalatedToModerator)
            .OrderByDescending(d => d.EscalatedAt)
            .ToListAsync();

        return disputes.Select(d => MapToListResponse(d, moderatorId, true)).ToList();
    }

    public async Task<DisputeResponse?> MakeModeratorDecisionAsync(Guid disputeId, ModeratorDecisionRequest request, Guid moderatorId)
    {
        var isModerator = await _dbContext.Users.AnyAsync(u => u.Id == moderatorId && u.IsModerator);
        if (!isModerator)
        {
            _logger.LogWarning("Moderator decision failed: User {UserId} is not a moderator", moderatorId);
            return null;
        }

        var dispute = await GetDisputeWithIncludesAsync(disputeId);

        if (dispute == null)
        {
            _logger.LogWarning("Moderator decision failed: Dispute {DisputeId} not found", disputeId);
            return null;
        }

        if (dispute.Status != DisputeStatus.EscalatedToModerator)
        {
            _logger.LogWarning("Moderator decision failed: Dispute {DisputeId} not escalated", disputeId);
            return null;
        }

        dispute.ModeratorId = moderatorId;
        dispute.ModeratorNotes = request.Notes;
        dispute.Resolution = request.Resolution;
        dispute.Status = DisputeStatus.Resolved;
        dispute.ClosedAt = DateTime.UtcNow;
        dispute.ResolutionSummary = $"Moderator decision: {request.Resolution}. {request.Notes}";

        if (request.Resolution == DisputeResolution.FavorsComplainer)
        {
            var reason = dispute.Score >= 40 && dispute.Score <= 60
                ? PenaltyReason.DisputeLostHalfPenalty
                : PenaltyReason.DisputeLostFullPenalty;
            CreatePenalty(dispute.RespondentId, dispute.AgreementId, dispute.Id, reason);
        }
        else if (request.Resolution == DisputeResolution.FavorsRespondent)
        {
            var reason = dispute.Score >= 40 && dispute.Score <= 60
                ? PenaltyReason.DisputeLostHalfPenalty
                : PenaltyReason.DisputeLostFullPenalty;
            CreatePenalty(dispute.OpenedById, dispute.AgreementId, dispute.Id, reason);
        }

        await ApplyResolutionToAgreement(dispute);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Dispute {DisputeId} resolved by moderator {ModeratorId} with {Resolution}",
            disputeId, moderatorId, request.Resolution);

        await NotifyDisputeResolutionAsync(dispute);

        return await MapToResponseAsync(dispute, moderatorId);
    }

    public async Task ProcessExpiredDisputesAsync()
    {
        var expiredDisputes = await _dbContext.Disputes
            .Include(d => d.Agreement)
            .Where(d => d.Status == DisputeStatus.AwaitingResponse &&
                       d.ResponseDeadline < DateTime.UtcNow)
            .ToListAsync();

        foreach (var dispute in expiredDisputes)
        {
            await EnforceResponseDeadlineAsync(dispute);
        }
    }

    private (int Score, bool ComplainerDelivered, bool RespondentDelivered, bool ComplainerOnTime, bool RespondentOnTime, bool ComplainerApprovedBeforeDispute, bool RespondentApprovedBeforeDispute) CalculateScoreData(
        Agreement agreement,
        Guid complainerId,
        Guid respondentId,
        DateTime deadline)
    {
        var complainerDeliverable = agreement.Deliverables.FirstOrDefault(d => d.SubmittedById == complainerId);
        var respondentDeliverable = agreement.Deliverables.FirstOrDefault(d => d.SubmittedById == respondentId);

        var complainerDelivered = complainerDeliverable is not null;
        var respondentDelivered = respondentDeliverable is not null;

        var complainerDue = complainerDeliverable?.Milestone?.DueAt ?? deadline;
        var respondentDue = respondentDeliverable?.Milestone?.DueAt ?? deadline;

        var complainerOnTime = complainerDeliverable is not null && complainerDeliverable.SubmittedAt <= complainerDue;
        var respondentOnTime = respondentDeliverable is not null && respondentDeliverable.SubmittedAt <= respondentDue;

        var complainerApproved = complainerDeliverable?.Status == DeliverableStatus.Approved;
        var respondentApproved = respondentDeliverable?.Status == DeliverableStatus.Approved;

        int score = 50;

        if (respondentDelivered && !complainerDelivered)
            score += 25;
        else if (complainerDelivered && !respondentDelivered)
            score -= 25;

        if (respondentOnTime && !complainerOnTime)
            score += 15;
        else if (complainerOnTime && !respondentOnTime)
            score -= 15;

        if (respondentApproved && !complainerApproved)
            score += 20;
        else if (complainerApproved && !respondentApproved)
            score -= 20;

        if (respondentDelivered && respondentOnTime)
            score += 5;
        if (complainerDelivered && complainerOnTime)
            score -= 5;

        score = Math.Clamp(score, 0, 100);

        return (score, complainerDelivered, respondentDelivered, complainerOnTime, respondentOnTime, complainerApproved, respondentApproved);
    }

    private static DisputeSystemDecision GetSystemDecisionFromScore(int score)
    {
        if (score >= 70)
            return DisputeSystemDecision.ProviderWins;
        if (score < 40)
            return DisputeSystemDecision.ComplainantWins;
        return DisputeSystemDecision.EscalateToModerator;
    }

    private static DisputeResolution GetResolutionForDecision(DisputeSystemDecision decision)
    {
        return decision switch
        {
            DisputeSystemDecision.ProviderWins => DisputeResolution.FavorsRespondent,
            DisputeSystemDecision.ComplainantWins => DisputeResolution.FavorsComplainer,
            _ => DisputeResolution.ModeratorDecision
        };
    }

    private void ResolveOrEscalate(Dispute dispute)
    {
        var decision = GetSystemDecisionFromScore(dispute.Score);
        dispute.SystemDecision = decision;
        dispute.Resolution = decision == DisputeSystemDecision.EscalateToModerator
            ? DisputeResolution.None
            : GetResolutionForDecision(decision);
        dispute.ComplainerDecision = DisputePartyDecision.Pending;
        dispute.RespondentDecision = DisputePartyDecision.Pending;

        if (decision == DisputeSystemDecision.EscalateToModerator)
        {
            dispute.Status = DisputeStatus.EscalatedToModerator;
            dispute.EscalatedAt = DateTime.UtcNow;
            dispute.ResolutionSummary = "Escalated to moderator: Score is in the gray zone (40-69).";
        }
        else
        {
            dispute.Status = DisputeStatus.UnderReview;
            dispute.ClosedAt = null;
            dispute.ResolutionSummary = decision == DisputeSystemDecision.ProviderWins
                ? "System decision favors the provider based on scoring."
                : "System decision favors the complainer based on scoring.";
        }
    }

    private async Task ApplyResolutionToAgreement(Dispute dispute)
    {
        var agreement = dispute.Agreement ?? await _dbContext.Agreements.FindAsync(dispute.AgreementId);

        if (agreement == null)
            return;

        agreement.Status = AgreementStatus.Cancelled;
    }

    private async Task<bool> EnforceResponseDeadlineAsync(Dispute dispute)
    {
        if (dispute.Status != DisputeStatus.AwaitingResponse || dispute.ResponseReceivedAt.HasValue)
            return false;

        if (DateTime.UtcNow <= dispute.ResponseDeadline)
            return false;

        dispute.Status = DisputeStatus.Resolved;
        dispute.Resolution = DisputeResolution.FavorsComplainer;
        dispute.SystemDecision = DisputeSystemDecision.ComplainantWins;
        dispute.ComplainerDecision = DisputePartyDecision.Accept;
        dispute.RespondentDecision = DisputePartyDecision.Reject;
        dispute.ClosedAt = DateTime.UtcNow;
        dispute.ResolutionSummary = "Auto-resolved: Respondent failed to respond within 72 hours.";

        CreatePenalty(dispute.RespondentId, dispute.AgreementId, dispute.Id, PenaltyReason.NoDisputeResponse);

        await ApplyResolutionToAgreement(dispute);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Dispute {DisputeId} auto-resolved due to no response", dispute.Id);

        await NotifyDisputeResolutionAsync(dispute);

        return true;
    }

    private async Task FinalizeSystemDecisionAsync(Dispute dispute)
    {
        if (dispute.Status != DisputeStatus.UnderReview)
            return;

        dispute.Status = DisputeStatus.Resolved;
        dispute.ClosedAt = DateTime.UtcNow;
        dispute.ResolutionSummary ??= "System decision accepted by both parties.";

        if (dispute.Resolution == DisputeResolution.FavorsComplainer)
        {
            CreatePenalty(dispute.RespondentId, dispute.AgreementId, dispute.Id, PenaltyReason.DisputeLostFullPenalty);
        }
        else if (dispute.Resolution == DisputeResolution.FavorsRespondent)
        {
            CreatePenalty(dispute.OpenedById, dispute.AgreementId, dispute.Id, PenaltyReason.DisputeLostFullPenalty);
        }

        await ApplyResolutionToAgreement(dispute);
        await _dbContext.SaveChangesAsync();
        await NotifyDisputeResolutionAsync(dispute);
    }

    private void SetEscalatedStatus(Dispute dispute, string? reason)
    {
        dispute.Status = DisputeStatus.EscalatedToModerator;
        dispute.SystemDecision = DisputeSystemDecision.EscalateToModerator;
        dispute.EscalatedAt = DateTime.UtcNow;
        dispute.Resolution = DisputeResolution.None;
        dispute.ResolutionSummary = string.IsNullOrWhiteSpace(reason)
            ? "Escalated to moderator by participant."
            : $"Escalated to moderator: {reason}";
        dispute.ClosedAt = null;
    }

    private async Task NotifyEscalationAsync(Dispute dispute)
    {
        await _notificationService.CreateAsync(
            dispute.OpenedById,
            NotificationType.DisputeEscalated,
            "Dispute Escalated",
            "The dispute has been escalated to a moderator for review"
        );
        await _notificationService.CreateAsync(
            dispute.RespondentId,
            NotificationType.DisputeEscalated,
            "Dispute Escalated",
            "The dispute has been escalated to a moderator for review"
        );
    }

    private async Task<Dispute?> GetDisputeWithIncludesAsync(Guid disputeId)
    {
        return await _dbContext.Disputes
            .Include(d => d.Agreement)
            .Include(d => d.OpenedBy)
            .Include(d => d.Respondent)
            .Include(d => d.Moderator)
            .Include(d => d.Evidence)
                .ThenInclude(e => e.SubmittedBy)
            .Include(d => d.Messages)
            .FirstOrDefaultAsync(d => d.Id == disputeId);
    }

    private static bool IsUserPartOfAgreement(Agreement agreement, Guid userId)
    {
        return agreement.RequesterId == userId || agreement.ProviderId == userId;
    }

    private static bool IsUserPartOfDispute(Dispute dispute, Guid userId)
    {
        return dispute.OpenedById == userId || dispute.RespondentId == userId;
    }

    private bool CanUserAddEvidence(Dispute dispute, Guid userId)
    {
        if (!IsUserPartOfDispute(dispute, userId))
            return false;

        return dispute.Status == DisputeStatus.AwaitingResponse ||
               dispute.Status == DisputeStatus.UnderReview ||
               dispute.Status == DisputeStatus.EscalatedToModerator;
    }

    private async Task<DisputeResponse> MapToResponseAsync(Dispute dispute, Guid currentUserId)
    {
        if (dispute.OpenedBy == null)
            await _dbContext.Entry(dispute).Reference(d => d.OpenedBy).LoadAsync();
        if (dispute.Respondent == null)
            await _dbContext.Entry(dispute).Reference(d => d.Respondent).LoadAsync();

        var evidenceEntry = _dbContext.Entry(dispute).Collection(d => d.Evidence);
        if (!evidenceEntry.IsLoaded)
            await evidenceEntry.LoadAsync();

        var interpretation = GetScoreInterpretation(dispute.Score);

        return new DisputeResponse
        {
            Id = dispute.Id,
            AgreementId = dispute.AgreementId,
            ReasonCode = dispute.ReasonCode,
            Description = dispute.Description,
            Status = dispute.Status,
            Resolution = dispute.Resolution,
            SystemDecision = dispute.SystemDecision,
            Score = dispute.Score,
            ScoreBreakdown = new ScoreBreakdown
            {
                ComplainerDelivered = dispute.ComplainerDelivered,
                RespondentDelivered = dispute.RespondentDelivered,
                ComplainerOnTime = dispute.ComplainerOnTime,
                RespondentOnTime = dispute.RespondentOnTime,
                ComplainerApprovedBeforeDispute = dispute.ComplainerApprovedBeforeDispute,
                RespondentApprovedBeforeDispute = dispute.RespondentApprovedBeforeDispute,
                Interpretation = interpretation
            },
            Complainer = new DisputePartyInfo
            {
                UserId = dispute.OpenedById,
                Name = dispute.OpenedBy?.Name ?? string.Empty
            },
            Respondent = new DisputePartyInfo
            {
                UserId = dispute.RespondentId,
                Name = dispute.Respondent?.Name ?? string.Empty
            },
            ComplainerDecision = dispute.ComplainerDecision,
            RespondentDecision = dispute.RespondentDecision,
            ResolutionSummary = dispute.ResolutionSummary,
            CreatedAt = dispute.CreatedAt,
            ResponseDeadline = dispute.ResponseDeadline,
            ResponseReceivedAt = dispute.ResponseReceivedAt,
            EscalatedAt = dispute.EscalatedAt,
            ClosedAt = dispute.ClosedAt,
            Evidence = dispute.Evidence?.Select(e => new EvidenceResponse
            {
                Id = e.Id,
                SubmittedById = e.SubmittedById,
                SubmittedByName = e.SubmittedBy?.Name ?? string.Empty,
                Link = e.Link,
                Description = e.Description,
                SubmittedAt = e.SubmittedAt
            }).ToList() ?? new List<EvidenceResponse>(),
            CanRespond = dispute.RespondentId == currentUserId && dispute.Status == DisputeStatus.AwaitingResponse,
            CanAddEvidence = CanUserAddEvidence(dispute, currentUserId),
            IsEscalated = dispute.Status == DisputeStatus.EscalatedToModerator,
            CanAcceptDecision = dispute.Status == DisputeStatus.UnderReview &&
                                IsUserPartOfDispute(dispute, currentUserId) &&
                                dispute.SystemDecision != DisputeSystemDecision.EscalateToModerator,
            CanEscalate = dispute.Status == DisputeStatus.UnderReview && IsUserPartOfDispute(dispute, currentUserId)
        };
    }

    private DisputeListResponse MapToListResponse(Dispute dispute, Guid currentUserId, bool isModeratorView = false)
    {
        return new DisputeListResponse
        {
            Id = dispute.Id,
            AgreementId = dispute.AgreementId,
            ReasonCode = dispute.ReasonCode,
            Status = dispute.Status,
            Resolution = dispute.Resolution,
            Score = dispute.Score,
            ComplainerName = dispute.OpenedBy?.Name ?? string.Empty,
            RespondentName = dispute.Respondent?.Name ?? string.Empty,
            CreatedAt = dispute.CreatedAt,
            ResponseDeadline = dispute.ResponseDeadline,
            RequiresAction =
                (dispute.RespondentId == currentUserId && dispute.Status == DisputeStatus.AwaitingResponse) ||
                (isModeratorView && dispute.Status == DisputeStatus.EscalatedToModerator)
        };
    }

    private static string GetScoreInterpretation(int score)
    {
        return score switch
        {
            >= 70 => "Strong evidence favors the respondent",
            >= 40 => "Evidence is inconclusive, requires moderator review",
            _ => "Strong evidence favors the complainer"
        };
    }

    private void CreatePenalty(Guid userId, Guid agreementId, Guid disputeId, PenaltyReason reason)
    {
        var amount = reason == PenaltyReason.DisputeLostHalfPenalty
            ? PenaltyConstants.HalfPenaltyAmount
            : PenaltyConstants.FullPenaltyAmount;

        var penalty = new Penalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AgreementId = agreementId,
            DisputeId = disputeId,
            Amount = amount,
            Currency = PenaltyConstants.DefaultCurrency,
            Reason = reason,
            Status = PenaltyStatus.Charged,
            CreatedAt = DateTime.UtcNow,
            ChargedAt = DateTime.UtcNow
        };

        _dbContext.Penalties.Add(penalty);
        _logger.LogInformation("Penalty charged for user {UserId} on agreement {AgreementId}: {Amount} {Currency} for {Reason}",
            userId, agreementId, amount, PenaltyConstants.DefaultCurrency, reason);

        _notificationService.CreateAsync(
            userId,
            NotificationType.PenaltyCharged,
            "Penalty Charged",
            $"A penalty of {amount} {PenaltyConstants.DefaultCurrency} has been charged"
        ).GetAwaiter().GetResult();
    }

    private async Task NotifyDisputeResolutionAsync(Dispute dispute)
    {
        var resolutionText = dispute.Resolution switch
        {
            DisputeResolution.FavorsComplainer => "in your favor",
            DisputeResolution.FavorsRespondent => "in favor of the other party",
            _ => "with a split decision"
        };

        await _notificationService.CreateAsync(
            dispute.OpenedById,
            NotificationType.DisputeResolved,
            "Dispute Resolved",
            $"Your dispute has been resolved {resolutionText}"
        );

        var respondentText = dispute.Resolution switch
        {
            DisputeResolution.FavorsComplainer => "in favor of the other party",
            DisputeResolution.FavorsRespondent => "in your favor",
            _ => "with a split decision"
        };

        await _notificationService.CreateAsync(
            dispute.RespondentId,
            NotificationType.DisputeResolved,
            "Dispute Resolved",
            $"The dispute has been resolved {respondentText}"
        );
    }
    public async Task<List<AdminDisputeListResponse>> GetAllActiveDisputesAsync()
    {
        var disputes = await _dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.Agreement)
            .Include(d => d.OpenedBy)
            .Include(d => d.Respondent)
            .Where(d => d.Status != DisputeStatus.Closed && d.Status != DisputeStatus.Resolved)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return disputes.Select(MapToAdminResponse).ToList();
    }

    public async Task<AdminDisputeListResponse?> GetDisputeForAdminAsync(Guid disputeId)
    {
        var dispute = await _dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.Agreement)
            .Include(d => d.OpenedBy)
            .Include(d => d.Respondent)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        return dispute == null ? null : MapToAdminResponse(dispute);
    }

    public async Task<AdminDisputeListResponse?> AdminResolveDisputeAsync(
        Guid disputeId,
        AdminResolveDisputeRequest request,
        Guid adminId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var dispute = await _dbContext.Disputes
                .Include(d => d.Agreement)
                .Include(d => d.OpenedBy)
                .Include(d => d.Respondent)
                .FirstOrDefaultAsync(d => d.Id == disputeId);

            if (dispute == null)
            {
                _logger.LogWarning("Admin resolve failed: Dispute {DisputeId} not found", disputeId);
                return null;
            }

            if (dispute.Status == DisputeStatus.Resolved || dispute.Status == DisputeStatus.Closed)
            {
                _logger.LogWarning("Admin resolve failed: Dispute {DisputeId} already resolved/closed", disputeId);
                return null;
            }

            dispute.ModeratorId = adminId;
            dispute.ModeratorNotes = request.ResolutionNote;
            dispute.Resolution = request.Resolution;
            dispute.Status = DisputeStatus.Resolved;
            dispute.ClosedAt = DateTime.UtcNow;
            dispute.ResolutionSummary = $"Admin resolution: {request.Resolution}. {request.ResolutionNote}";

            if (request.Resolution == DisputeResolution.FavorsComplainer)
            {
                var reason = dispute.Score >= 40 && dispute.Score <= 60
                    ? PenaltyReason.DisputeLostHalfPenalty
                    : PenaltyReason.DisputeLostFullPenalty;
                CreatePenalty(dispute.RespondentId, dispute.AgreementId, dispute.Id, reason);
            }
            else if (request.Resolution == DisputeResolution.FavorsRespondent)
            {
                var reason = dispute.Score >= 40 && dispute.Score <= 60
                    ? PenaltyReason.DisputeLostHalfPenalty
                    : PenaltyReason.DisputeLostFullPenalty;
                CreatePenalty(dispute.OpenedById, dispute.AgreementId, dispute.Id, reason);
            }

            if (dispute.Agreement != null)
            {
                dispute.Agreement.Status = request.UpdateAgreementStatus ?? AgreementStatus.Cancelled;
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Dispute {DisputeId} resolved by admin {AdminId} with resolution {Resolution}",
                disputeId, adminId, request.Resolution);

            await NotifyDisputeResolutionAsync(dispute);

            return MapToAdminResponse(dispute);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error resolving dispute {DisputeId} by admin {AdminId}", disputeId, adminId);
            throw;
        }
    }

    private static AdminDisputeListResponse MapToAdminResponse(Dispute dispute)
    {
        return new AdminDisputeListResponse
        {
            Id = dispute.Id,
            ReasonCode = dispute.ReasonCode,
            Description = dispute.Description,
            Status = dispute.Status,
            Resolution = dispute.Resolution,
            Score = dispute.Score,
            ResolutionSummary = dispute.ResolutionSummary,
            CreatedAt = dispute.CreatedAt,
            ResponseDeadline = dispute.ResponseDeadline,
            ResponseReceivedAt = dispute.ResponseReceivedAt,
            EscalatedAt = dispute.EscalatedAt,
            ClosedAt = dispute.ClosedAt,
            Agreement = new AdminDisputeAgreementInfo
            {
                Id = dispute.Agreement?.Id ?? Guid.Empty,
                Terms = dispute.Agreement?.Terms,
                Status = dispute.Agreement?.Status ?? AgreementStatus.Pending,
                CreatedAt = dispute.Agreement?.CreatedAt ?? DateTime.MinValue,
                AcceptedAt = dispute.Agreement?.AcceptedAt
            },
            Complainer = new AdminDisputeUserInfo
            {
                Id = dispute.OpenedById,
                Name = dispute.OpenedBy?.Name ?? string.Empty,
                Email = dispute.OpenedBy?.Email ?? string.Empty
            },
            Respondent = new AdminDisputeUserInfo
            {
                Id = dispute.RespondentId,
                Name = dispute.Respondent?.Name ?? string.Empty,
                Email = dispute.Respondent?.Email ?? string.Empty
            }
        };
    }
}
