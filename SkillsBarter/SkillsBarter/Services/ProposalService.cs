using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class ProposalService : IProposalService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAgreementService _agreementService;
    private readonly ILogger<ProposalService> _logger;

    public ProposalService(
        ApplicationDbContext dbContext,
        IAgreementService agreementService,
        ILogger<ProposalService> logger)
    {
        _dbContext = dbContext;
        _agreementService = agreementService;
        _logger = logger;
    }

    public async Task<ProposalResponse?> CreateProposalAsync(CreateProposalRequest request, Guid proposerId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Validate offer exists and is active
            var offer = await _dbContext.Offers
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == request.OfferId);

            if (offer == null)
            {
                _logger.LogWarning("Create proposal failed: Offer {OfferId} not found", request.OfferId);
                return null;
            }

            if (offer.StatusCode != OfferStatusCode.Active)
            {
                _logger.LogWarning("Create proposal failed: Offer {OfferId} is not active (Status: {Status})",
                    request.OfferId, offer.StatusCode);
                return null;
            }

            // Validate proposer exists and is not the offer owner
            var proposer = await _dbContext.Users.FindAsync(proposerId);
            if (proposer == null)
            {
                _logger.LogWarning("Create proposal failed: Proposer {ProposerId} not found", proposerId);
                return null;
            }

            if (offer.UserId == proposerId)
            {
                _logger.LogWarning("Create proposal failed: User {UserId} cannot propose on their own offer", proposerId);
                return null;
            }

            // Validate deadline is in the future
            if (request.Deadline <= DateTime.UtcNow)
            {
                _logger.LogWarning("Create proposal failed: Deadline must be in the future");
                return null;
            }

            var existingProposal = await _dbContext.Proposals
                .Where(p => p.OfferId == request.OfferId &&
                           p.ProposerId == proposerId &&
                           (p.Status == ProposalStatus.PendingOfferOwnerReview ||
                            p.Status == ProposalStatus.PendingOfferOwnerReview))
                .FirstOrDefaultAsync();

            if (existingProposal != null)
            {
                _logger.LogWarning("Create proposal failed: User {UserId} already has a pending proposal {ProposalId} for offer {OfferId}",
                    proposerId, existingProposal.Id, request.OfferId);
                return null;
            }

            var proposal = new Proposal
            {
                Id = Guid.NewGuid(),
                OfferId = request.OfferId,
                ProposerId = proposerId,
                OfferOwnerId = offer.UserId,
                Terms = request.Terms,
                ProposerOffer = request.ProposerOffer,
                Deadline = request.Deadline,
                Status = ProposalStatus.PendingOfferOwnerReview,
                PendingResponseFromUserId = offer.UserId,
                ModificationCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Proposals.Add(proposal);

            var historyEntry = new ProposalHistory
            {
                Id = Guid.NewGuid(),
                ProposalId = proposal.Id,
                ActorId = proposerId,
                Action = ProposalAction.Created,
                Terms = request.Terms,
                ProposerOffer = request.ProposerOffer,
                Deadline = request.Deadline,
                Message = request.Message,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ProposalHistories.Add(historyEntry);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Proposal {ProposalId} created by user {ProposerId} for offer {OfferId}",
                proposal.Id, proposerId, request.OfferId);

            return await MapToProposalResponseAsync(proposal);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating proposal for offer {OfferId}", request.OfferId);
            throw;
        }
    }

    public async Task<ProposalResponse?> RespondToProposalAsync(Guid proposalId, RespondToProposalRequest request, Guid responderId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var proposal = await _dbContext.Proposals
                .Include(p => p.Offer)
                .Include(p => p.Proposer)
                .Include(p => p.OfferOwner)
                .FirstOrDefaultAsync(p => p.Id == proposalId);

            if (proposal == null)
            {
                _logger.LogWarning("Respond to proposal failed: Proposal {ProposalId} not found", proposalId);
                return null;
            }

            // Validate responder is part of the proposal
            if (proposal.ProposerId != responderId && proposal.OfferOwnerId != responderId)
            {
                _logger.LogWarning("Respond to proposal failed: User {UserId} is not part of proposal {ProposalId}",
                    responderId, proposalId);
                return null;
            }

            // Validate it's the responder's turn
            if (proposal.PendingResponseFromUserId != responderId)
            {
                _logger.LogWarning("Respond to proposal failed: Not user {UserId}'s turn to respond to proposal {ProposalId}",
                    responderId, proposalId);
                return null;
            }

            // Validate proposal is in a respondable state
            if (proposal.Status != ProposalStatus.PendingOfferOwnerReview &&
                proposal.Status != ProposalStatus.PendingOfferOwnerReview)
            {
                _logger.LogWarning("Respond to proposal failed: Proposal {ProposalId} is not pending response (Status: {Status})",
                    proposalId, proposal.Status);
                return null;
            }

            switch (request.Action)
            {
                case ProposalResponseAction.Accept:
                    await HandleAcceptAsync(proposal, responderId, request.Message);
                    break;

                case ProposalResponseAction.Modify:
                    if (string.IsNullOrWhiteSpace(request.Terms) || string.IsNullOrWhiteSpace(request.ProposerOffer))
                    {
                        _logger.LogWarning("Respond to proposal failed: Terms and ProposerOffer are required for modification");
                        return null;
                    }
                    HandleModify(proposal, responderId, request);
                    break;

                case ProposalResponseAction.Decline:
                    HandleDecline(proposal, responderId, request.Message);
                    break;
            }

            // Create history entry
            var historyEntry = new ProposalHistory
            {
                Id = Guid.NewGuid(),
                ProposalId = proposal.Id,
                ActorId = responderId,
                Action = request.Action switch
                {
                    ProposalResponseAction.Accept => ProposalAction.Accepted,
                    ProposalResponseAction.Modify => ProposalAction.Modified,
                    ProposalResponseAction.Decline => ProposalAction.Declined,
                    _ => throw new ArgumentOutOfRangeException()
                },
                Terms = proposal.Terms,
                ProposerOffer = proposal.ProposerOffer,
                Deadline = proposal.Deadline,
                Message = request.Message,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ProposalHistories.Add(historyEntry);
            _dbContext.Proposals.Update(proposal);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Proposal {ProposalId} responded to by user {UserId} with action {Action}",
                proposalId, responderId, request.Action);

            return await MapToProposalResponseAsync(proposal);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error responding to proposal {ProposalId}", proposalId);
            throw;
        }
    }

    private async Task HandleAcceptAsync(Proposal proposal, Guid responderId, string? message)
    {
        proposal.Status = ProposalStatus.Accepted;
        proposal.AcceptedAt = DateTime.UtcNow;
        proposal.PendingResponseFromUserId = null;

        var agreementResult = await _agreementService.CreateAgreementAsync(
            proposal.OfferId,
            proposal.ProposerId,  
            proposal.OfferOwnerId, 
            proposal.Terms);

        if (agreementResult != null)
        {
            proposal.AgreementId = agreementResult.Id;
            _logger.LogInformation("Agreement {AgreementId} created from accepted proposal {ProposalId}",
                agreementResult.Id, proposal.Id);
        }
        else
        {
            _logger.LogWarning("Failed to create agreement from proposal {ProposalId}", proposal.Id);
        }
    }

    private void HandleModify(Proposal proposal, Guid responderId, RespondToProposalRequest request)
    {
        proposal.Terms = request.Terms!;
        proposal.ProposerOffer = request.ProposerOffer!;

        if (request.Deadline.HasValue && request.Deadline.Value > DateTime.UtcNow)
        {
            proposal.Deadline = request.Deadline.Value;
        }

        proposal.ModificationCount++;
        proposal.LastModifiedByUserId = responderId;
        proposal.LastModifiedAt = DateTime.UtcNow;

        // Switch whose turn it is - the other party needs to respond
        proposal.PendingResponseFromUserId = responderId == proposal.OfferOwnerId
            ? proposal.ProposerId
            : proposal.OfferOwnerId;
    }

    private void HandleDecline(Proposal proposal, Guid responderId, string? message)
    {
        proposal.Status = ProposalStatus.Declined;
        proposal.DeclineReason = message;
        proposal.PendingResponseFromUserId = null;
    }

    public async Task<bool> WithdrawProposalAsync(Guid proposalId, Guid userId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var proposal = await _dbContext.Proposals.FindAsync(proposalId);

            if (proposal == null)
            {
                _logger.LogWarning("Withdraw proposal failed: Proposal {ProposalId} not found", proposalId);
                return false;
            }

            if (proposal.ProposerId != userId)
            {
                _logger.LogWarning("Withdraw proposal failed: User {UserId} is not the proposer of proposal {ProposalId}",
                    userId, proposalId);
                return false;
            }

            if (proposal.Status != ProposalStatus.PendingOfferOwnerReview &&
                proposal.Status != ProposalStatus.PendingOfferOwnerReview)
            {
                _logger.LogWarning("Withdraw proposal failed: Proposal {ProposalId} is not in a withdrawable state (Status: {Status})",
                    proposalId, proposal.Status);
                return false;
            }

            proposal.Status = ProposalStatus.Withdrawn;
            proposal.PendingResponseFromUserId = null;

            var historyEntry = new ProposalHistory
            {
                Id = Guid.NewGuid(),
                ProposalId = proposal.Id,
                ActorId = userId,
                Action = ProposalAction.Withdrawn,
                Terms = proposal.Terms,
                ProposerOffer = proposal.ProposerOffer,
                Deadline = proposal.Deadline,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ProposalHistories.Add(historyEntry);
            _dbContext.Proposals.Update(proposal);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Proposal {ProposalId} withdrawn by user {UserId}", proposalId, userId);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error withdrawing proposal {ProposalId}", proposalId);
            throw;
        }
    }

    public async Task<ProposalResponse?> GetProposalByIdAsync(Guid proposalId)
    {
        var proposal = await _dbContext.Proposals
            .Include(p => p.Offer)
            .Include(p => p.Proposer)
            .Include(p => p.OfferOwner)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            return null;
        }

        return await MapToProposalResponseAsync(proposal);
    }

    public async Task<ProposalDetailResponse?> GetProposalDetailByIdAsync(Guid proposalId)
    {
        var proposal = await _dbContext.Proposals
            .Include(p => p.Offer)
                .ThenInclude(o => o.Skill)
            .Include(p => p.Proposer)
            .Include(p => p.OfferOwner)
            .Include(p => p.History)
                .ThenInclude(h => h.Actor)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            return null;
        }

        return MapToProposalDetailResponse(proposal);
    }

    public async Task<ProposalListResponse> GetUserProposalsAsync(Guid userId, GetProposalsRequest request)
    {
        var query = _dbContext.Proposals
            .Include(p => p.Offer)
            .Include(p => p.Proposer)
            .Include(p => p.OfferOwner)
            .AsQueryable();

        if (request.AsSender == true && request.AsReceiver != true)
        {
            query = query.Where(p => p.ProposerId == userId);
        }
        else if (request.AsReceiver == true && request.AsSender != true)
        {
            query = query.Where(p => p.OfferOwnerId == userId);
        }
        else
        {
            query = query.Where(p => p.ProposerId == userId || p.OfferOwnerId == userId);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(p => p.Status == request.Status.Value);
        }

        if (request.OfferId.HasValue)
        {
            query = query.Where(p => p.OfferId == request.OfferId.Value);
        }

        var totalCount = await query.CountAsync();

        var proposals = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var proposalResponses = new List<ProposalResponse>();
        foreach (var proposal in proposals)
        {
            proposalResponses.Add(await MapToProposalResponseAsync(proposal));
        }

        return new ProposalListResponse
        {
            Proposals = proposalResponses,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
        };
    }

    public async Task<ProposalListResponse> GetOfferProposalsAsync(Guid offerId, GetProposalsRequest request)
    {
        var query = _dbContext.Proposals
            .Include(p => p.Offer)
            .Include(p => p.Proposer)
            .Include(p => p.OfferOwner)
            .Where(p => p.OfferId == offerId);

        if (request.Status.HasValue)
        {
            query = query.Where(p => p.Status == request.Status.Value);
        }

        var totalCount = await query.CountAsync();

        var proposals = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var proposalResponses = new List<ProposalResponse>();
        foreach (var proposal in proposals)
        {
            proposalResponses.Add(await MapToProposalResponseAsync(proposal));
        }

        return new ProposalListResponse
        {
            Proposals = proposalResponses,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
        };
    }

    public async Task<bool> CanUserRespondAsync(Guid proposalId, Guid userId)
    {
        var proposal = await _dbContext.Proposals.FindAsync(proposalId);

        if (proposal == null)
        {
            return false;
        }

        return proposal.PendingResponseFromUserId == userId &&
               (proposal.Status == ProposalStatus.PendingOfferOwnerReview ||
                proposal.Status == ProposalStatus.PendingOfferOwnerReview);
    }

    public async Task<int> MarkExpiredProposalsAsync()
    {
        var expiredProposals = await _dbContext.Proposals
            .Where(p => p.Deadline < DateTime.UtcNow &&
                       (p.Status == ProposalStatus.PendingOfferOwnerReview ||
                        p.Status == ProposalStatus.PendingOfferOwnerReview))
            .ToListAsync();

        foreach (var proposal in expiredProposals)
        {
            proposal.Status = ProposalStatus.Expired;
            proposal.PendingResponseFromUserId = null;

            var historyEntry = new ProposalHistory
            {
                Id = Guid.NewGuid(),
                ProposalId = proposal.Id,
                ActorId = Guid.Empty, // System action
                Action = ProposalAction.Expired,
                Terms = proposal.Terms,
                ProposerOffer = proposal.ProposerOffer,
                Deadline = proposal.Deadline,
                Message = "Proposal expired due to deadline",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ProposalHistories.Add(historyEntry);
        }

        if (expiredProposals.Any())
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Marked {Count} proposals as expired", expiredProposals.Count);
        }

        return expiredProposals.Count;
    }

    private async Task<ProposalResponse> MapToProposalResponseAsync(Proposal proposal)
    {
        if (proposal.Offer == null)
        {
            await _dbContext.Entry(proposal).Reference(p => p.Offer).LoadAsync();
        }
        if (proposal.Proposer == null)
        {
            await _dbContext.Entry(proposal).Reference(p => p.Proposer).LoadAsync();
        }
        if (proposal.OfferOwner == null)
        {
            await _dbContext.Entry(proposal).Reference(p => p.OfferOwner).LoadAsync();
        }

        return new ProposalResponse
        {
            Id = proposal.Id,
            OfferId = proposal.OfferId,
            OfferTitle = proposal.Offer?.Title ?? string.Empty,
            ProposerId = proposal.ProposerId,
            ProposerName = proposal.Proposer?.Name ?? string.Empty,
            OfferOwnerId = proposal.OfferOwnerId,
            OfferOwnerName = proposal.OfferOwner?.Name ?? string.Empty,
            Terms = proposal.Terms,
            ProposerOffer = proposal.ProposerOffer,
            Deadline = proposal.Deadline,
            Status = proposal.Status,
            PendingResponseFromUserId = proposal.PendingResponseFromUserId,
            ModificationCount = proposal.ModificationCount,
            CreatedAt = proposal.CreatedAt,
            AcceptedAt = proposal.AcceptedAt,
            AgreementId = proposal.AgreementId
        };
    }

    private ProposalDetailResponse MapToProposalDetailResponse(Proposal proposal)
    {
        return new ProposalDetailResponse
        {
            Id = proposal.Id,
            OfferId = proposal.OfferId,
            OfferTitle = proposal.Offer?.Title ?? string.Empty,
            ProposerId = proposal.ProposerId,
            ProposerName = proposal.Proposer?.Name ?? string.Empty,
            OfferOwnerId = proposal.OfferOwnerId,
            OfferOwnerName = proposal.OfferOwner?.Name ?? string.Empty,
            Terms = proposal.Terms,
            ProposerOffer = proposal.ProposerOffer,
            Deadline = proposal.Deadline,
            Status = proposal.Status,
            PendingResponseFromUserId = proposal.PendingResponseFromUserId,
            ModificationCount = proposal.ModificationCount,
            CreatedAt = proposal.CreatedAt,
            AcceptedAt = proposal.AcceptedAt,
            AgreementId = proposal.AgreementId,
            Proposer = new ProposalUserInfo
            {
                Id = proposal.Proposer?.Id ?? Guid.Empty,
                Name = proposal.Proposer?.Name ?? string.Empty,
                VerificationLevel = proposal.Proposer?.VerificationLevel ?? 0,
                ReputationScore = proposal.Proposer?.ReputationScore ?? 0
            },
            OfferOwner = new ProposalUserInfo
            {
                Id = proposal.OfferOwner?.Id ?? Guid.Empty,
                Name = proposal.OfferOwner?.Name ?? string.Empty,
                VerificationLevel = proposal.OfferOwner?.VerificationLevel ?? 0,
                ReputationScore = proposal.OfferOwner?.ReputationScore ?? 0
            },
            Offer = new ProposalOfferInfo
            {
                Id = proposal.Offer?.Id ?? Guid.Empty,
                Title = proposal.Offer?.Title ?? string.Empty,
                Description = proposal.Offer?.Description,
                SkillName = proposal.Offer?.Skill?.Name ?? string.Empty
            },
            History = proposal.History
                .OrderBy(h => h.CreatedAt)
                .Select(h => new ProposalHistoryInfo
                {
                    Id = h.Id,
                    ActorId = h.ActorId,
                    ActorName = h.Actor?.Name ?? "System",
                    Action = h.Action,
                    Terms = h.Terms,
                    ProposerOffer = h.ProposerOffer,
                    Deadline = h.Deadline,
                    Message = h.Message,
                    CreatedAt = h.CreatedAt
                }).ToList()
        };
    }
}
