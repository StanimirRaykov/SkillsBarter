namespace SkillsBarter.Constants;

public static class NotificationType
{
    public const string ProposalReceived = "proposal_received";
    public const string ProposalAccepted = "proposal_accepted";
    public const string ProposalDeclined = "proposal_declined";
    public const string ProposalModified = "proposal_modified";
    public const string ProposalWithdrawn = "proposal_withdrawn";

    public const string AgreementCreated = "agreement_created";
    public const string AgreementCompleted = "agreement_completed";
    public const string AgreementCancelled = "agreement_cancelled";

    public const string DeliverableSubmitted = "deliverable_submitted";
    public const string DeliverableApproved = "deliverable_approved";
    public const string RevisionRequested = "revision_requested";

    public const string DisputeOpened = "dispute_opened";
    public const string DisputeResponse = "dispute_response";
    public const string DisputeEscalated = "dispute_escalated";
    public const string DisputeResolved = "dispute_resolved";

    public const string PenaltyCharged = "penalty_charged";

    public const string ReviewReceived = "review_received";
}
