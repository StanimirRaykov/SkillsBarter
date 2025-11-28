namespace SkillsBarter.Models;

public class Review
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid AgreementId { get; set; }
    public short Rating { get; set; }
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser Recipient { get; set; } = null!;
    public virtual ApplicationUser Reviewer { get; set; } = null!;
    public virtual Agreement Agreement { get; set; } = null!;
}
