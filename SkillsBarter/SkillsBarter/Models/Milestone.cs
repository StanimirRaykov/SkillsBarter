namespace SkillsBarter.Models;

public class Milestone
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }

    public Guid ResponsibleUserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public int DurationInDays { get; set; }
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Pending;
    public DateTime? DueAt { get; set; }

    public virtual Agreement Agreement { get; set; } = null!;
    public virtual ApplicationUser ResponsibleUser { get; set; } = null!;
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
