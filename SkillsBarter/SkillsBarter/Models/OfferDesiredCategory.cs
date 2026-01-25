namespace SkillsBarter.Models;

public class OfferDesiredCategory
{
    public Guid OfferId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;

    public virtual Offer Offer { get; set; } = null!;
    public virtual SkillCategory Category { get; set; } = null!;
}
