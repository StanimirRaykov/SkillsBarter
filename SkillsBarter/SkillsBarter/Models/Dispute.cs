namespace SkillsBarter.Models;

public class Dispute
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid OpenedById { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ResolutionSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public virtual Agreement Agreement { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    public virtual ApplicationUser OpenedBy { get; set; } = null!;
    public virtual ICollection<DisputeMessage> Messages { get; set; } = new List<DisputeMessage>();
}
