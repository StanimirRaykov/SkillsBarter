using System.ComponentModel.DataAnnotations;
using SkillsBarter.Models;

namespace SkillsBarter.DTOs;

public class OpenDisputeRequest
{
    [Required]
    public Guid AgreementId { get; set; }

    [Required]
    public DisputeReasonCode ReasonCode { get; set; }

    [Required]
    [MinLength(20)]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public List<EvidenceRequest> Evidence { get; set; } = new();
}

public class EvidenceRequest
{
    [Required]
    [Url]
    public string Link { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}

public class RespondToDisputeRequest
{
    [Required]
    [MinLength(20)]
    [MaxLength(2000)]
    public string Response { get; set; } = string.Empty;

    public List<EvidenceRequest> Evidence { get; set; } = new();
}

public class AcceptDecisionRequest
{
    [Required]
    public bool Accept { get; set; }
}

public class EscalateDisputeRequest
{
    [MaxLength(2000)]
    public string? Reason { get; set; }
}

public class ModeratorDecisionRequest
{
    [Required]
    public DisputeResolution Resolution { get; set; }

    [Required]
    [MinLength(20)]
    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;
}

public class DisputeResponse
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public DisputeReasonCode ReasonCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; }
    public DisputeResolution Resolution { get; set; }
    public DisputeSystemDecision SystemDecision { get; set; }

    public int Score { get; set; }
    public ScoreBreakdown ScoreBreakdown { get; set; } = new();

    public DisputePartyInfo Complainer { get; set; } = new();
    public DisputePartyInfo Respondent { get; set; } = new();
    public DisputePartyDecision ComplainerDecision { get; set; }
    public DisputePartyDecision RespondentDecision { get; set; }

    public string? ResolutionSummary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ResponseDeadline { get; set; }
    public DateTime? ResponseReceivedAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public List<EvidenceResponse> Evidence { get; set; } = new();

    public bool CanRespond { get; set; }
    public bool CanAddEvidence { get; set; }
    public bool IsEscalated { get; set; }
    public bool CanAcceptDecision { get; set; }
    public bool CanEscalate { get; set; }
}

public class ScoreBreakdown
{
    public bool ComplainerDelivered { get; set; }
    public bool RespondentDelivered { get; set; }
    public bool ComplainerOnTime { get; set; }
    public bool RespondentOnTime { get; set; }
    public bool ComplainerApprovedBeforeDispute { get; set; }
    public bool RespondentApprovedBeforeDispute { get; set; }
    public string Interpretation { get; set; } = string.Empty;
}

public class DisputePartyInfo
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class EvidenceResponse
{
    public Guid Id { get; set; }
    public Guid SubmittedById { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}

public class DisputeListResponse
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public DisputeReasonCode ReasonCode { get; set; }
    public DisputeStatus Status { get; set; }
    public DisputeResolution Resolution { get; set; }
    public int Score { get; set; }
    public string ComplainerName { get; set; } = string.Empty;
    public string RespondentName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ResponseDeadline { get; set; }
    public bool RequiresAction { get; set; }
}

public class AdminDisputeListResponse
{
    public Guid Id { get; set; }
    public DisputeReasonCode ReasonCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; }
    public DisputeResolution Resolution { get; set; }
    public int Score { get; set; }
    public string? ResolutionSummary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ResponseDeadline { get; set; }
    public DateTime? ResponseReceivedAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public AdminDisputeAgreementInfo Agreement { get; set; } = new();

    public AdminDisputeUserInfo Complainer { get; set; } = new();
    public AdminDisputeUserInfo Respondent { get; set; } = new();
}

public class AdminDisputeAgreementInfo
{
    public Guid Id { get; set; }
    public string? Terms { get; set; }
    public AgreementStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

public class AdminDisputeUserInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class AdminResolveDisputeRequest
{
    [Required]
    public DisputeResolution Resolution { get; set; }

    [Required]
    [MinLength(10)]
    [MaxLength(2000)]
    public string ResolutionNote { get; set; } = string.Empty;

    public AgreementStatus? UpdateAgreementStatus { get; set; }
}
