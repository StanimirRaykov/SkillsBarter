namespace SkillsBarter.DTOs;

public class SkillResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? CategoryLabel { get; set; }
}
