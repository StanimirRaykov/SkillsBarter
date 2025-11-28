namespace SkillsBarter.Models;

public class Agreement
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public Guid BuyerId { get; set; }
    public Guid SellerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual Offer Offer { get; set; } = null!;
    public virtual ApplicationUser Buyer { get; set; } = null!;
    public virtual ApplicationUser Seller { get; set; } = null!;
    public virtual ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
