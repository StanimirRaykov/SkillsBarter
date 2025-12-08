using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IAgreementService
{
    Task<AgreementResponse?> CreateAgreementAsync(
        Guid offerId,
        Guid requesterId,
        Guid providerId,
        string? terms
    );
    Task<AgreementResponse?> CompleteAgreementAsync(Guid agreementId, Guid userId);
    Task<AgreementResponse?> GetAgreementByIdAsync(Guid agreementId);
    Task<AgreementDetailResponse?> GetAgreementDetailByIdAsync(Guid agreementId);
    Task ProcessAbandonedAgreementsAsync();
}
