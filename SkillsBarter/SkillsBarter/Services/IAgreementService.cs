using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public interface IAgreementService
{
    Task<AgreementResponse?> CreateAgreementAsync(
        Guid offerId,
        Guid requesterId,
        Guid providerId,
        string? terms,
        List<CreateMilestoneRequest>? milestones = null
    );
    Task<AgreementResponse?> CompleteAgreementAsync(Guid agreementId, Guid userId);
    Task<AgreementResponse?> GetAgreementByIdAsync(Guid agreementId);
    Task<AgreementDetailResponse?> GetAgreementDetailByIdAsync(Guid agreementId);
    Task<AgreementListResponse> GetUserAgreementsAsync(Guid userId, AgreementStatus? status = null, int page = 1, int pageSize = 10);
    Task ProcessAbandonedAgreementsAsync();
}
