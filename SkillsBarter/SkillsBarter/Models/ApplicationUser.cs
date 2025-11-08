using Microsoft.AspNetCore.Identity;

namespace SkillsBarter.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public short VerificationLevel { get; set; } = 0;
    public decimal ReputationScore { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
    public virtual ICollection<RequestThread> InitiatedThreads { get; set; } = new List<RequestThread>();
    public virtual ICollection<RequestThread> ReceivedThreads { get; set; } = new List<RequestThread>();
    public virtual ICollection<RequestMessage> SentMessages { get; set; } = new List<RequestMessage>();
    public virtual ICollection<Agreement> BuyerAgreements { get; set; } = new List<Agreement>();
    public virtual ICollection<Agreement> SellerAgreements { get; set; } = new List<Agreement>();
    public virtual ICollection<Payment> PayerPayments { get; set; } = new List<Payment>();
    public virtual ICollection<Payment> PayeePayments { get; set; } = new List<Payment>();
    public virtual ICollection<Dispute> OpenedDisputes { get; set; } = new List<Dispute>();
    public virtual ICollection<DisputeMessage> DisputeMessages { get; set; } = new List<DisputeMessage>();
    public virtual ICollection<Review> ReviewsGiven { get; set; } = new List<Review>();
    public virtual ICollection<Review> ReviewsReceived { get; set; } = new List<Review>();
}
