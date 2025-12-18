namespace SkillsBarter.DTOs;

public class OfferResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public decimal? Rating { get; set; }
    public int ReviewCount { get; set; }
    public int SkillId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? StatusLabel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
