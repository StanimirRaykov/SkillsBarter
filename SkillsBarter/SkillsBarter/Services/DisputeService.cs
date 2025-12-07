using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class DisputeService : IDisputeService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DisputeService> _logger;
    private const int ResponseDeadlineHours = 72;

    public DisputeService(ApplicationDbContext dbContext, ILogger<DisputeService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DisputeResponse?> OpenDisputeAsync(OpenDisputeRequest request, Guid userId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var agreement = await _dbContext.Agreements
                .Include(a => a.Deliverables)
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

            var dispute = new Dispute
            {
                Id = Guid.NewGuid(),
                AgreementId = request.AgreementId,
                OpenedById = userId,
                RespondentId = respondentId,
                ReasonCode = request.ReasonCode,
                Description = request.Description,
                Status = DisputeStatus.AwaitingResponse,
                Resolution = DisputeResolution.None,
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

            return await MapToResponseAsync(dispute, userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error responding to dispute {DisputeId} by user {UserId}", disputeId, userId);
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

        return await MapToResponseAsync(dispute, userId);
    }

    public async Task<List<DisputeListResponse>> GetMyDisputesAsync(Guid userId)
    {
        var disputes = await _dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.OpenedBy)
            .Include(d => d.Respondent)
            .Where(d => d.OpenedById == userId || d.RespondentId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return disputes.Select(d => MapToListResponse(d, userId)).ToList();
    }

    public async Task<List<DisputeListResponse>> GetDisputesForModerationAsync(Guid moderatorId)
    {
        var isModerator = await _dbContext.Users.AsNoTracking().AnyAsync(u => u.Id == moderatorId && u.IsModerator);
        if (!isModerator)
            return new List<DisputeListResponse>();

        var disputes = await _dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.OpenedBy)
            .Include(d => d.Respondent)
            .Where(d => d.Status == DisputeStatus.EscalatedToModerator)
            .OrderByDescending(d => d.EscalatedAt)
            .ToListAsync();

        return disputes.Select(d => MapToListResponse(d, moderatorId)).ToList();
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

        await ApplyResolutionToAgreement(dispute);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Dispute {DisputeId} resolved by moderator {ModeratorId} with {Resolution}",
            disputeId, moderatorId, request.Resolution);

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
            dispute.Status = DisputeStatus.Resolved;
            dispute.Resolution = DisputeResolution.FavorsComplainer;
            dispute.ClosedAt = DateTime.UtcNow;
            dispute.ResolutionSummary = "Auto-resolved: Respondent failed to respond within 72 hours.";

            await ApplyResolutionToAgreement(dispute);

            _logger.LogInformation("Dispute {DisputeId} auto-resolved due to no response", dispute.Id);
        }

        if (expiredDisputes.Count > 0)
            await _dbContext.SaveChangesAsync();
    }

    private (int Score, bool ComplainerDelivered, bool RespondentDelivered, bool ComplainerOnTime, bool RespondentOnTime, bool ComplainerApprovedBeforeDispute, bool RespondentApprovedBeforeDispute) CalculateScoreData(
        Agreement agreement,
        Guid complainerId,
        Guid respondentId,
        DateTime deadline)
    {
        var complainerDeliverable = agreement.Deliverables.FirstOrDefault(d => d.SubmittedById == complainerId);
        var respondentDeliverable = agreement.Deliverables.FirstOrDefault(d => d.SubmittedById == respondentId);

        bool complainerDelivered = complainerDeliverable != null;
        bool respondentDelivered = respondentDeliverable != null;

        bool complainerOnTime = complainerDeliverable != null && complainerDeliverable.SubmittedAt <= deadline;
        bool respondentOnTime = respondentDeliverable != null && respondentDeliverable.SubmittedAt <= deadline;

        bool complainerApproved = complainerDeliverable?.Status == DeliverableStatus.Approved;
        bool respondentApproved = respondentDeliverable?.Status == DeliverableStatus.Approved;

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

    private void ResolveOrEscalate(Dispute dispute)
    {
        if (dispute.Score >= 70)
        {
            dispute.Status = DisputeStatus.Resolved;
            dispute.Resolution = DisputeResolution.FavorsRespondent;
            dispute.ClosedAt = DateTime.UtcNow;
            dispute.ResolutionSummary = "Auto-resolved: Score indicates respondent fulfilled obligations (score >= 70).";
        }
        else if (dispute.Score < 40)
        {
            dispute.Status = DisputeStatus.Resolved;
            dispute.Resolution = DisputeResolution.FavorsComplainer;
            dispute.ClosedAt = DateTime.UtcNow;
            dispute.ResolutionSummary = "Auto-resolved: Score indicates complainer's grievance is valid (score < 40).";
        }
        else
        {
            dispute.Status = DisputeStatus.EscalatedToModerator;
            dispute.EscalatedAt = DateTime.UtcNow;
            dispute.ResolutionSummary = "Escalated to moderator: Score is in the gray zone (40-69).";
        }
    }

    private async Task ApplyResolutionToAgreement(Dispute dispute)
    {
        var agreement = dispute.Agreement ?? await _dbContext.Agreements.FindAsync(dispute.AgreementId);

        if (agreement == null)
            return;

        agreement.Status = AgreementStatus.Cancelled;
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
            IsEscalated = dispute.Status == DisputeStatus.EscalatedToModerator
        };
    }

    private DisputeListResponse MapToListResponse(Dispute dispute, Guid currentUserId)
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
            RequiresAction = dispute.RespondentId == currentUserId && dispute.Status == DisputeStatus.AwaitingResponse
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
}
