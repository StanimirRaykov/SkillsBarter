using System.ComponentModel.DataAnnotations;

namespace SkillsBarter.DTOs;

public class UserPreferencesResponse
{
    public bool Success { get; set; }
    public UserPreferences Preferences { get; set; } = new();
}

public class UserPreferences
{
    public NotificationPreferences? Notifications { get; set; }
    public PrivacyPreferences? Privacy { get; set; }
    public GeneralPreferences? General { get; set; }
}

public class NotificationPreferences
{
    public bool EmailNotifications { get; set; } = true;
    public bool ProposalNotifications { get; set; } = true;
    public bool AgreementNotifications { get; set; } = true;
    public bool MessageNotifications { get; set; } = true;
    public bool ReviewNotifications { get; set; } = true;
    public bool DisputeNotifications { get; set; } = true;
    public string NotificationFrequency { get; set; } = "instant";
}

public class PrivacyPreferences
{
    public string ProfileVisibility { get; set; } = "public";
    public bool ShowEmail { get; set; } = false;
    public string MessagePermissions { get; set; } = "anyone";
    public bool ShowOnlineStatus { get; set; } = true;
}

public class GeneralPreferences
{
    public string Language { get; set; } = "en";
    public string Timezone { get; set; } = "UTC";
    public string DateFormat { get; set; } = "MM/DD/YYYY";
}

public class UpdatePreferencesRequest
{
    public NotificationPreferences? Notifications { get; set; }
    public PrivacyPreferences? Privacy { get; set; }
    public GeneralPreferences? General { get; set; }
}

public class ChangeEmailRequest
{
    [Required(ErrorMessage = "New email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string NewEmail { get; set; } = string.Empty;
}

public class ConfirmEmailChangeRequest
{
    [Required(ErrorMessage = "Token is required")]
    public string Token { get; set; } = string.Empty;
}
