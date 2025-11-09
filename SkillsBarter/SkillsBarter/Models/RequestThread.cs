namespace SkillsBarter.Models;

public class RequestThread
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public Guid InitiatorId { get; set; }
    public Guid RecipientId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Offer Offer { get; set; } = null!;
    public virtual ApplicationUser Initiator { get; set; } = null!;
    public virtual ApplicationUser Recipient { get; set; } = null!;
    public virtual ICollection<RequestMessage> Messages { get; set; } = new List<RequestMessage>();
}
