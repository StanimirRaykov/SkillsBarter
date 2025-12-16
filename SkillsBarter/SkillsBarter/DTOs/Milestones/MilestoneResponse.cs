using SkillsBarter.Models;

namespace SkillsBarter.DTOs;

public class MilestoneResponse
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DurationInDays { get; set; }
    public MilestoneStatus Status { get; set; }
    public DateTime? DueAt { get; set; }
}
