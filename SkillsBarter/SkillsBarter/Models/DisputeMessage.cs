namespace SkillsBarter.Models;

public class DisputeMessage
{
    public Guid Id { get; set; }
    public Guid DisputeId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Dispute Dispute { get; set; } = null!;
    public virtual ApplicationUser Sender { get; set; } = null!;
}
