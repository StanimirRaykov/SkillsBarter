namespace SkillsBarter.DTOs;

public class CreateOfferRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid SkillId { get; set; }
}
