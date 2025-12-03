using SkillsBarter.DTOs;

namespace SkillsBarter.Services;

public interface IReviewService
{
    Task<ReviewResponse?> CreateReviewAsync(Guid reviewerId, CreateReviewRequest request);
    Task<PaginatedResponse<ReviewResponse>> GetReviewsForUserAsync(Guid userId, int page = 1, int pageSize = 10);
    Task<UserReviewsWithSummaryResponse> GetUserReviewsWithSummaryAsync(Guid userId, int page = 1, int pageSize = 10);
}
