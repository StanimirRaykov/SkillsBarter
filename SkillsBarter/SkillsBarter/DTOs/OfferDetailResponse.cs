namespace SkillsBarter.DTOs;

public class OfferDetailResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SkillId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? StatusLabel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public OfferOwnerInfo Owner { get; set; } = null!;
}

public class OfferOwnerInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Rating { get; set; }
}
