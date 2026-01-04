using System.ComponentModel.DataAnnotations;

namespace SkillsBarter.DTOs;

public class AdminUpdateUserRoleRequest
{
    [Required(ErrorMessage = "Role is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Role must be between 1 and 50 characters")]
    public string Role { get; set; } = string.Empty;
}
