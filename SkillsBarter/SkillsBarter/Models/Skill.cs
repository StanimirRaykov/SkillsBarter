namespace SkillsBarter.Models;

public class Skill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;

    public virtual SkillCategory Category { get; set; } = null!;
    public virtual ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
}
