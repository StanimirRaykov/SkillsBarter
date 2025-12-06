using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IDeliverableService
{
    Task<DeliverableResponse?> SubmitDeliverableAsync(SubmitDeliverableRequest request, Guid userId);
    Task<DeliverableResponse?> ApproveDeliverableAsync(Guid deliverableId, Guid userId);
    Task<DeliverableResponse?> RequestRevisionAsync(Guid deliverableId, RequestRevisionRequest request, Guid userId);
    Task<DeliverableResponse?> GetDeliverableByIdAsync(Guid deliverableId, Guid userId);
    Task<AgreementDeliverablesResponse?> GetAgreementDeliverablesAsync(Guid agreementId, Guid userId);
    Task<DeliverableResponse?> ResubmitDeliverableAsync(Guid deliverableId, SubmitDeliverableRequest request, Guid userId);
}
