using System.ComponentModel.DataAnnotations;
using SkillsBarter.Models;

namespace SkillsBarter.DTOs;

public class CreateProposalRequest
{
    [Required]
    public Guid OfferId { get; set; }

    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string Terms { get; set; } = string.Empty;

    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string ProposerOffer { get; set; } = string.Empty;

    [Required]
    public DateTime Deadline { get; set; }

    [StringLength(500)]
    public string? Message { get; set; }

    [Required]
    public List<CreateMilestoneRequest> Milestones { get; set; } = new();
}

public class RespondToProposalRequest
{
    [Required]
    public ProposalResponseAction Action { get; set; }

    [StringLength(2000)]
    public string? Terms { get; set; }

    [StringLength(2000)]
    public string? ProposerOffer { get; set; }

    public DateTime? Deadline { get; set; }

    [StringLength(500)]
    public string? Message { get; set; }
    public List<CreateMilestoneRequest>? Milestones { get; set; }
}

public enum ProposalResponseAction
{
    Accept,
    Modify,
    Decline
}

public class GetProposalsRequest
{
    public Guid? OfferId { get; set; }
    public ProposalStatus? Status { get; set; }
    public bool? AsSender { get; set; }
    public bool? AsReceiver { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}


public class ProposalResponse
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public string OfferTitle { get; set; } = string.Empty;
    public Guid ProposerId { get; set; }
    public string ProposerName { get; set; } = string.Empty;
    public Guid OfferOwnerId { get; set; }
    public string OfferOwnerName { get; set; } = string.Empty;
    public string Terms { get; set; } = string.Empty;
    public string ProposerOffer { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public ProposalStatus Status { get; set; }
    public Guid? PendingResponseFromUserId { get; set; }
    public int ModificationCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? AgreementId { get; set; }
}

public class ProposalDetailResponse : ProposalResponse
{
    public ProposalUserInfo Proposer { get; set; } = null!;
    public ProposalUserInfo OfferOwner { get; set; } = null!;
    public ProposalOfferInfo Offer { get; set; } = null!;
    public List<ProposalHistoryInfo> History { get; set; } = new();
}

public class ProposalUserInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public short VerificationLevel { get; set; }
    public decimal ReputationScore { get; set; }
}

public class ProposalOfferInfo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SkillName { get; set; } = string.Empty;
}

public class ProposalHistoryInfo
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public ProposalAction Action { get; set; }
    public string Terms { get; set; } = string.Empty;
    public string ProposerOffer { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProposalListResponse
{
    public List<ProposalResponse> Proposals { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
