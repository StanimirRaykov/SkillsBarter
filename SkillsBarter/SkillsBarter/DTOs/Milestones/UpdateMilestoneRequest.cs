namespace SkillsBarter.DTOs;

public class UpdateMilestoneRequest
{
    public string? Title { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? DueAt { get; set; }
}
