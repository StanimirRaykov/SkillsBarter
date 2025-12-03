namespace SkillsBarter.DTOs;

public class UpdateOfferRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? SkillId { get; set; }
    public string? StatusCode { get; set; }
}
