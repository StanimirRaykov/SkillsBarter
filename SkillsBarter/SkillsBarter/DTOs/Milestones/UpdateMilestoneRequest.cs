namespace SkillsBarter.DTOs;

public class UpdateMilestoneRequest
{
    public string? Title { get; set; }
    public int? DurationInDays { get; set; }
    public DateTime? DueAt { get; set; }
}
