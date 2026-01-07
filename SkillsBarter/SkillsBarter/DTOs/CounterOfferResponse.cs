namespace SkillsBarter.DTOs;

public class CounterOfferResponse
{
    public Guid Id { get; set; }
    public Guid ProposalId { get; set; }
    public string Terms { get; set; } = null!;
    public string? Message { get; set; }
    public string CreatedByName { get; set; } = null!;
    public CounterOfferStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
