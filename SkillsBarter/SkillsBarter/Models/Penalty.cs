namespace SkillsBarter.Models;

public class Penalty
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AgreementId { get; set; }
    public Guid? DisputeId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public PenaltyReason Reason { get; set; }
    public PenaltyStatus Status { get; set; } = PenaltyStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ChargedAt { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Agreement Agreement { get; set; } = null!;
    public virtual Dispute? Dispute { get; set; }
}
