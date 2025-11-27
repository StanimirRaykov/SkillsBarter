using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IUserService
{
    Task<PublicUserProfileResponse?> GetPublicProfileAsync(Guid userId);
}
