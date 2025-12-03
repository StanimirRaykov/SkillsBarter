namespace SkillsBarter.DTOs;

public class PublicUserProfileResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public short VerificationLevel { get; set; }
    public decimal ReputationScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<UserSkillDto> Skills { get; set; } = new();
    public UserStatsDto Stats { get; set; } = new();
}

public class UserSkillDto
{
    public int SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public int ActiveOffersCount { get; set; }
}

public class UserStatsDto
{
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int TotalActiveOffers { get; set; }
}
