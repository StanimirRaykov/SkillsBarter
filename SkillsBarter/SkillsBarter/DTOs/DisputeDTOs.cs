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

    public int Score { get; set; }
    public ScoreBreakdown ScoreBreakdown { get; set; } = new();

    public DisputePartyInfo Complainer { get; set; } = new();
    public DisputePartyInfo Respondent { get; set; } = new();

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
