namespace SkillsBarter.DTOs;

public class UserReviewsWithSummaryResponse
{
    public ReviewSummary Summary { get; set; } = new();
    public PaginatedResponse<ReviewResponse> Reviews { get; set; } = new();
}

public class ReviewSummary
{
    public int TotalReviews { get; set; }
    public decimal AverageRating { get; set; }
}
