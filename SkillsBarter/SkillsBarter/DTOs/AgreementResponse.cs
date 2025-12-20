using SkillsBarter.Models;

namespace SkillsBarter.DTOs;

public class AgreementResponse
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
}

public class AgreementSummaryResponse
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public string OfferTitle { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public AgreementStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalMilestones { get; set; }
    public int CompletedMilestones { get; set; }
    public string Role { get; set; } = string.Empty; // "Requester" or "Provider"
    public bool HasReviewFromMe { get; set; }
}

public class AgreementListResponse
{
    public List<AgreementSummaryResponse> Agreements { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class AdminAgreementSummaryResponse
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public string OfferTitle { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public AgreementStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalMilestones { get; set; }
    public int CompletedMilestones { get; set; }
    public int TotalDeliverables { get; set; }
    public int ApprovedDeliverables { get; set; }
    public bool HasDispute { get; set; }
}

public class AdminAgreementListResponse
{
    public List<AdminAgreementSummaryResponse> Agreements { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
