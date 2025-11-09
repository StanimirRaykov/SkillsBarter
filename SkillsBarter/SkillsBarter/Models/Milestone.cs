namespace SkillsBarter.Models;

public class Milestone
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }

    public virtual Agreement Agreement { get; set; } = null!;
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
