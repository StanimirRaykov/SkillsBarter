using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IOfferService
{
    Task<(bool IsAllowed, string? ErrorMessage)> CheckOfferCreationAllowedAsync(Guid userId);
    Task<OfferResponse?> CreateOfferAsync(Guid userId, CreateOfferRequest request);
    Task<PaginatedResponse<OfferResponse>> GetOffersAsync(GetOffersRequest request);
    Task<PaginatedResponse<OfferResponse>> GetMyOffersAsync(Guid userId, GetOffersRequest request);
    Task<OfferDetailResponse?> GetOfferByIdAsync(Guid offerId, Guid? userId = null);
    Task<OfferResponse?> UpdateOfferAsync(Guid offerId, Guid userId, UpdateOfferRequest request, bool isAdmin);
    Task<bool> DeleteOfferAsync(Guid offerId, Guid userId, bool isAdmin);
}
