using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IOfferService
{
    Task<OfferResponse?> CreateOfferAsync(Guid userId, CreateOfferRequest request);
    Task<PaginatedResponse<OfferResponse>> GetOffersAsync(GetOffersRequest request);
    Task<OfferDetailResponse?> GetOfferByIdAsync(Guid offerId);
}
