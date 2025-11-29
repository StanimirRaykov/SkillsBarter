namespace SkillsBarter.DTOs;

public class ReviewResponse
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public Guid ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public Guid AgreementId { get; set; }
    public short Rating { get; set; }
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; }
}
