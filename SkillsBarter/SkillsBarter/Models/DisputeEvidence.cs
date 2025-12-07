namespace SkillsBarter.Models;

public class DisputeEvidence
{
    public Guid Id { get; set; }
    public Guid DisputeId { get; set; }
    public Guid SubmittedById { get; set; }
    public string Link { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public virtual Dispute Dispute { get; set; } = null!;
    public virtual ApplicationUser SubmittedBy { get; set; } = null!;
}
