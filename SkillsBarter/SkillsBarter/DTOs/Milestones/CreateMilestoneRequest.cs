namespace SkillsBarter.DTOs;

public class CreateMilestoneRequest
{
    public string Title { get; set; } = string.Empty;
    public int DurationInDays { get; set; }
    public DateTime? DueAt { get; set; }

    public Guid? ResponsibleUserId { get; set; }
}
