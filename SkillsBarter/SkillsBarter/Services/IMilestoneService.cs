using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IMilestoneService
{
    Task<MilestoneResponse?> CreateMilestoneAsync(Guid agreementId, CreateMilestoneRequest request, Guid currentUserId);
    Task<MilestoneResponse?> GetMilestoneByIdAsync(Guid milestoneId, Guid currentUserId);
    Task<List<MilestoneResponse>> GetMilestonesByAgreementIdAsync(Guid agreementId, Guid currentUserId);
    Task<MilestoneResponse?> UpdateMilestoneAsync(Guid milestoneId, UpdateMilestoneRequest request, Guid currentUserId);
    Task<bool> DeleteMilestoneAsync(Guid milestoneId, Guid currentUserId);
    Task<MilestoneResponse?> MarkMilestoneAsCompletedAsync(Guid milestoneId, Guid currentUserId);
}
