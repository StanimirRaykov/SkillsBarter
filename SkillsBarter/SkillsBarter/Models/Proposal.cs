namespace SkillsBarter.Models;

public class Proposal
{
    public Guid Id { get; set; }

    public Guid OfferId { get; set; }

    public Guid ProposerId { get; set; }

    public Guid OfferOwnerId { get; set; }

    public string Terms { get; set; } = string.Empty;

    public string ProposerOffer { get; set; } = string.Empty;

    public DateTime Deadline { get; set; }

    public ProposalStatus Status { get; set; } = ProposalStatus.PendingOfferOwnerReview;

    public Guid? PendingResponseFromUserId { get; set; }

    public int ModificationCount { get; set; } = 0;

    public Guid? LastModifiedByUserId { get; set; }

    public DateTime? LastModifiedAt { get; set; }

    public string? DeclineReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? AcceptedAt { get; set; }

    public Guid? AgreementId { get; set; }

    public virtual Offer Offer { get; set; } = null!;
    public virtual ApplicationUser Proposer { get; set; } = null!;
    public virtual ApplicationUser OfferOwner { get; set; } = null!;
    public virtual ApplicationUser? PendingResponseFromUser { get; set; }
    public virtual ApplicationUser? LastModifiedByUser { get; set; }
    public virtual Agreement? Agreement { get; set; }
    public virtual ICollection<ProposalHistory> History { get; set; } = new List<ProposalHistory>();
}
