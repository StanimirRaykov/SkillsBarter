namespace SkillsBarter.Models;

public class Deliverable
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public Guid SubmittedById { get; set; }
    public Guid? MilestoneId { get; set; }
    public string Link { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DeliverableStatus Status { get; set; } = DeliverableStatus.Submitted;
    public string? RevisionReason { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public int RevisionCount { get; set; }

    public virtual Agreement Agreement { get; set; } = null!;
    public virtual ApplicationUser SubmittedBy { get; set; } = null!;
    public virtual Milestone? Milestone { get; set; }
}
