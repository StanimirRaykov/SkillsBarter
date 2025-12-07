using System.ComponentModel.DataAnnotations;
using SkillsBarter.Models;

namespace SkillsBarter.DTOs;

public class SubmitDeliverableRequest
{
    [Required]
    public Guid AgreementId { get; set; }

    [Required]
    [Url]
    [StringLength(2000)]
    public string Link { get; set; } = string.Empty;

    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;
}

public class RequestRevisionRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;
}

public class DeliverableResponse
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public Guid SubmittedById { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DeliverableStatus Status { get; set; }
    public string? RevisionReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int RevisionCount { get; set; }
    public bool CanApprove { get; set; }
    public bool CanRequestRevision { get; set; }
}

public class AgreementDeliverablesResponse
{
    public Guid AgreementId { get; set; }
    public DeliverableResponse? RequesterDeliverable { get; set; }
    public DeliverableResponse? ProviderDeliverable { get; set; }
    public bool BothApproved { get; set; }
}
