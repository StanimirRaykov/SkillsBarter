using System.ComponentModel.DataAnnotations;
using SkillsBarter.Models;

namespace SkillsBarter.DTOs;

public class SubmitDeliverableRequest
{
    [Required]
    public Guid AgreementId { get; set; }

    [Required]
    [Display(Name = "milestone")]
    public Guid MilestoneId { get; set; }

    [Required]
    [Url(ErrorMessage = "Link must be a valid URL (e.g., https://github.com/...)")]
    [StringLength(2000)]
    public string Link { get; set; } = string.Empty;

    [Required]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
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
    public Guid? MilestoneId { get; set; }
    public string? MilestoneTitle { get; set; }
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
    public List<MilestoneDeliverableResponse> Milestones { get; set; } = new();
    public bool AllApproved { get; set; }
}

public class MilestoneDeliverableResponse
{
    public Guid MilestoneId { get; set; }
    public string MilestoneTitle { get; set; } = string.Empty;
    public MilestoneStatus MilestoneStatus { get; set; }
    public int DurationInDays { get; set; }
    public DateTime? DueAt { get; set; }
    public Guid ResponsibleUserId { get; set; }
    public string ResponsibleUserName { get; set; } = string.Empty;
    public DeliverableResponse? Deliverable { get; set; }
}
