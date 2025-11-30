namespace SkillsBarter.DTOs;

public class AgreementResponse
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid ProviderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
