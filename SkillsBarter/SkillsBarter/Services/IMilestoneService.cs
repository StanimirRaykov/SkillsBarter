using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IMilestoneService
{
    Task<MilestoneResponse?> CreateMilestoneAsync(Guid agreementId, CreateMilestoneRequest request);
    Task<MilestoneResponse?> GetMilestoneByIdAsync(Guid milestoneId);
    Task<List<MilestoneResponse>> GetMilestonesByAgreementIdAsync(Guid agreementId);
    Task<MilestoneResponse?> UpdateMilestoneAsync(Guid milestoneId, UpdateMilestoneRequest request);
    Task<bool> DeleteMilestoneAsync(Guid milestoneId);
    Task<MilestoneResponse?> MarkMilestoneAsCompletedAsync(Guid milestoneId);
}
