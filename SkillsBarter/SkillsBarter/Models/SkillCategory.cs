namespace SkillsBarter.Models;

public class SkillCategory
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
