using System.ComponentModel.DataAnnotations;

namespace SkillsBarter.DTOs;

public class CreateReviewRequest
{
    [Required(ErrorMessage = "Recipient ID is required")]
    public Guid RecipientId { get; set; }

    [Required(ErrorMessage = "Agreement ID is required")]
    public Guid AgreementId { get; set; }

    [Required(ErrorMessage = "Rating is required")]
    [Range(1, 11, ErrorMessage = "Rating must be between 1 and 11")]
    public short Rating { get; set; }

    [MaxLength(1000, ErrorMessage = "Review body cannot exceed 1000 characters")]
    public string? Body { get; set; }
}
