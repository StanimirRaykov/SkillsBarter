namespace SkillsBarter.DTOs;

public class DetailedUserProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PhoneNumber { get; set; }
    public short VerificationLevel { get; set; }
    public decimal ReputationScore { get; set; }
    public bool IsModerator { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<UserSkillDto> Skills { get; set; } = new();
    public UserStatsDto Stats { get; set; } = new();
}
