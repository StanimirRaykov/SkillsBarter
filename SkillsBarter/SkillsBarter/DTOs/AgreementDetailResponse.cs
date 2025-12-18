using SkillsBarter.Models;

namespace SkillsBarter.DTOs;

public class AgreementDetailResponse
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid ProviderId { get; set; }
    public string? Terms { get; set; }
    public AgreementStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public AgreementUserInfo Requester { get; set; } = null!;
    public AgreementUserInfo Provider { get; set; } = null!;
    public AgreementOfferInfo Offer { get; set; } = null!;
    public List<MilestoneInfo> Milestones { get; set; } = new();
    public List<PaymentInfo> Payments { get; set; } = new();
    public List<ReviewInfo> Reviews { get; set; } = new();
}

public class AgreementUserInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public short VerificationLevel { get; set; }
    public decimal ReputationScore { get; set; }
}

public class AgreementOfferInfo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
}

public class MilestoneInfo
{
    public Guid Id { get; set; }
    public Guid ResponsibleUserId { get; set; }
    public string ResponsibleUserName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int DurationInDays { get; set; }
    public MilestoneStatus Status { get; set; }
    public DateTime? DueAt { get; set; }
}

public class PaymentInfo
{
    public Guid Id { get; set; }
    public Guid? MilestoneId { get; set; }
    public Guid TipFromUserId { get; set; }
    public Guid TipToUserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ReviewInfo
{
    public Guid Id { get; set; }
    public Guid ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public Guid RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public short Rating { get; set; }
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; }
}
