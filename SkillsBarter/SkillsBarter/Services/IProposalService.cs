using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public interface IProposalService
{
    Task<ProposalResponse?> CreateProposalAsync(CreateProposalRequest request, Guid proposerId);
    Task<ProposalResponse?> RespondToProposalAsync(Guid proposalId, RespondToProposalRequest request, Guid responderId);
    Task<bool> WithdrawProposalAsync(Guid proposalId, Guid userId);
    Task<ProposalResponse?> GetProposalByIdAsync(Guid proposalId);
    Task<ProposalDetailResponse?> GetProposalDetailByIdAsync(Guid proposalId);
    Task<ProposalListResponse> GetUserProposalsAsync(Guid userId, GetProposalsRequest request);
    Task<ProposalListResponse> GetOfferProposalsAsync(Guid offerId, GetProposalsRequest request);
    Task<bool> CanUserRespondAsync(Guid proposalId, Guid userId);
    Task<int> MarkExpiredProposalsAsync();
}
