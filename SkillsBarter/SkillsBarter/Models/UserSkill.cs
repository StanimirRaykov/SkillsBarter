namespace SkillsBarter.Models;

public class UserSkill
{
    public Guid UserId { get; set; }
    public int SkillId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}
