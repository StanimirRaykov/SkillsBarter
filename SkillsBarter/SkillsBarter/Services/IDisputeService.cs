using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IDisputeService
{
    Task<DisputeResponse?> OpenDisputeAsync(OpenDisputeRequest request, Guid userId);
    Task<DisputeResponse?> RespondToDisputeAsync(Guid disputeId, RespondToDisputeRequest request, Guid userId);
    Task<DisputeResponse?> AddEvidenceAsync(Guid disputeId, EvidenceRequest request, Guid userId);
    Task<DisputeResponse?> GetDisputeByIdAsync(Guid disputeId, Guid userId);
    Task<List<DisputeListResponse>> GetMyDisputesAsync(Guid userId);
    Task<List<DisputeListResponse>> GetDisputesForModerationAsync(Guid moderatorId);
    Task<DisputeResponse?> MakeModeratorDecisionAsync(Guid disputeId, ModeratorDecisionRequest request, Guid moderatorId);
    Task ProcessExpiredDisputesAsync();
    Task<List<AdminDisputeListResponse>> GetAllActiveDisputesAsync();
    Task<AdminDisputeListResponse?> GetDisputeForAdminAsync(Guid disputeId);
    Task<AdminDisputeListResponse?> AdminResolveDisputeAsync(Guid disputeId, AdminResolveDisputeRequest request, Guid adminId);
}
