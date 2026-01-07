namespace SkillsBarter.Models;

public class CounterOffer
{
    public Guid Id { get; set; }

    public Guid ProposalId { get; set; }
    public Proposal Proposal { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;

    public string Terms { get; set; } = null!;
    public string? Message { get; set; }

    public CounterOfferStatus Status { get; set; } = CounterOfferStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
