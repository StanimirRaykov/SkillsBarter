namespace SkillsBarter.Models;

public class OfferStatus
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
}
