namespace SkillsBarter.Models;

public class Dispute
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid OpenedById { get; set; }
    public Guid RespondentId { get; set; }

    public DisputeReasonCode ReasonCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public DisputeResolution Resolution { get; set; } = DisputeResolution.None;

    public int Score { get; set; }
    public bool ComplainerDelivered { get; set; }
    public bool RespondentDelivered { get; set; }
    public bool ComplainerOnTime { get; set; }
    public bool RespondentOnTime { get; set; }
    public bool ComplainerApprovedBeforeDispute { get; set; }
    public bool RespondentApprovedBeforeDispute { get; set; }

    public string? ResolutionSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ResponseDeadline { get; set; }
    public DateTime? ResponseReceivedAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public Guid? ModeratorId { get; set; }
    public string? ModeratorNotes { get; set; }

    public virtual Agreement Agreement { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    public virtual ApplicationUser OpenedBy { get; set; } = null!;
    public virtual ApplicationUser Respondent { get; set; } = null!;
    public virtual ApplicationUser? Moderator { get; set; }
    public virtual ICollection<DisputeMessage> Messages { get; set; } = new List<DisputeMessage>();
    public virtual ICollection<DisputeEvidence> Evidence { get; set; } = new List<DisputeEvidence>();
}
