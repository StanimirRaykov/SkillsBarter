namespace SkillsBarter.Models;

public class ProposalHistory
{
    public Guid Id { get; set; }

    public Guid ProposalId { get; set; }

    public Guid ActorId { get; set; }

    public ProposalAction Action { get; set; }

    public string Terms { get; set; } = string.Empty;

    public string ProposerOffer { get; set; } = string.Empty;

    public DateTime Deadline { get; set; }

    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Proposal Proposal { get; set; } = null!;
    public virtual ApplicationUser Actor { get; set; } = null!;
}

public enum ProposalAction
{
    Created,
    Modified,
    Accepted,
    Declined,
    Withdrawn,
    Expired
}
