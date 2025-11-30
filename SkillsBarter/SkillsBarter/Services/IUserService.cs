using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IUserService
{
    Task<PublicUserProfileResponse?> GetPublicProfileAsync(Guid userId);
    Task<DetailedUserProfileResponse?> GetDetailedProfileAsync(Guid userId);
}
