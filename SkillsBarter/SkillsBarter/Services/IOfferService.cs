using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IOfferService
{
    Task<OfferResponse?> CreateOfferAsync(Guid userId, CreateOfferRequest request);
}
