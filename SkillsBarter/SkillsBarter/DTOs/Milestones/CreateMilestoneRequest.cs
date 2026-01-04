using System.ComponentModel.DataAnnotations;

namespace SkillsBarter.DTOs;

public class CreateMilestoneRequest
{
    [Required(ErrorMessage = "Milestone title is required")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
    public string Title { get; set; } = string.Empty;

    [Range(1, 365, ErrorMessage = "Duration must be between 1 and 365 days")]
    public int DurationInDays { get; set; }

    public DateTime? DueAt { get; set; }

    public Guid? ResponsibleUserId { get; set; }
}
