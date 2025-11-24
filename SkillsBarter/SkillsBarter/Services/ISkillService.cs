using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface ISkillService
{
    Task<SkillResponse?> CreateSkillAsync(CreateSkillRequest request);
    Task<PaginatedResponse<SkillResponse>> GetSkillsAsync(GetSkillsRequest request);
    Task<SkillResponse?> GetSkillByIdAsync(Guid id);
}
