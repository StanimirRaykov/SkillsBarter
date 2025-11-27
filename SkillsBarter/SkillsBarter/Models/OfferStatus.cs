namespace SkillsBarter.Models;

public class OfferStatus
{
    public OfferStatusCode Code { get; set; }
    public string Label { get; set; } = string.Empty;

    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
}
