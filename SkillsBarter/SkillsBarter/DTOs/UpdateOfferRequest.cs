namespace SkillsBarter.DTOs;

public class UpdateOfferRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid? SkillId { get; set; }
    public string? StatusCode { get; set; }
}
