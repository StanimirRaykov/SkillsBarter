using System.ComponentModel.DataAnnotations;

namespace SkillsBarter.DTOs;

public class CreateSkillRequest
{
    [Required(ErrorMessage = "Skill name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Skill name must be between 2 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category code is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Category code must be between 1 and 50 characters")]
    public string CategoryCode { get; set; } = string.Empty;
}
