using System.ComponentModel.DataAnnotations;

namespace SkillsBarter.DTOs;

public class CreateOfferRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "SkillId must be a valid skill")]
    public int SkillId { get; set; }
}
