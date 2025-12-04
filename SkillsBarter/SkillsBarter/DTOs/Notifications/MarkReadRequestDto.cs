namespace SkillsBarter.DTOs.Notifications;

public class MarkReadRequestDto
{
    public List<Guid>? Ids { get; set; }
    public bool MarkAll { get; set; }
}

