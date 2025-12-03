using System.ComponentModel.DataAnnotations;

namespace SkillsBarter.DTOs;

public class UpdateProfileRequest
{
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string? Name { get; set; }

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? PhoneNumber { get; set; }

    public List<int>? SkillIds { get; set; }
}
