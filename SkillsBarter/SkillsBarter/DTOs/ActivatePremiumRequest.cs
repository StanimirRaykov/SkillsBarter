using System.ComponentModel.DataAnnotations;

namespace SkillsBarter.DTOs;

public class ActivatePremiumRequest
{
    [Required(ErrorMessage = "Subscription ID is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Subscription ID must be between 1 and 100 characters")]
    public string SubscriptionId { get; set; } = string.Empty;
}
