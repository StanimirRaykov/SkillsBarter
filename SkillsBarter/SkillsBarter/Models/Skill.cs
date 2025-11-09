namespace SkillsBarter.Models;

public class Skill
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;

    public virtual SkillCategory Category { get; set; } = null!;
    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
}
