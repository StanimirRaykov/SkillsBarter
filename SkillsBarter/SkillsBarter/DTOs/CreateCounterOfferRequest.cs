namespace SkillsBarter.DTOs;

public class CreateCounterOfferRequest
{
    public string Terms { get; set; } = null!;
    public string? Message { get; set; }
}
