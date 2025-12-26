using System.ComponentModel.DataAnnotations;

namespace SkillsBarter.DTOs;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long")]
    public string NewPassword { get; set; } = string.Empty;
}
