namespace SkillsBarter.Models;

public class Offer
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SkillId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public OfferStatusCode StatusCode { get; set; } = OfferStatusCode.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
    public virtual OfferStatus Status { get; set; } = null!;
    public virtual ICollection<RequestThread> RequestThreads { get; set; } = new List<RequestThread>();
    public virtual ICollection<Agreement> Agreements { get; set; } = new List<Agreement>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
