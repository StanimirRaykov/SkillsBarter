namespace SkillsBarter.DTOs;

public class CreateMilestoneRequest
{
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? DueAt { get; set; }
}
