using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IOfferService
{
    Task<OfferResponse?> CreateOfferAsync(Guid userId, CreateOfferRequest request);
    Task<PaginatedResponse<OfferResponse>> GetOffersAsync(GetOffersRequest request);
    Task<PaginatedResponse<OfferResponse>> GetMyOffersAsync(Guid userId, GetOffersRequest request);
    Task<OfferDetailResponse?> GetOfferByIdAsync(Guid offerId);
    Task<OfferResponse?> UpdateOfferAsync(Guid offerId, Guid userId, UpdateOfferRequest request, bool isAdmin);
    Task<bool> DeleteOfferAsync(Guid offerId, Guid userId, bool isAdmin);
}
