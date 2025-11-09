namespace SkillsBarter.Models;

public class Payment
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public Guid? MilestoneId { get; set; }
    public Guid PayerId { get; set; }
    public Guid PayeeId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentType { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ProviderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Agreement Agreement { get; set; } = null!;
    public virtual Milestone? Milestone { get; set; }
    public virtual ApplicationUser Payer { get; set; } = null!;
    public virtual ApplicationUser Payee { get; set; } = null!;
    public virtual ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
}
