using Resend;

namespace SkillsBarter.Services;

public class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IResend resend, IConfiguration configuration, ILogger<EmailService> logger)
    {
        _resend = resend;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string userName, string verificationToken)
    {
        try
        {
            var appUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
            var verificationUrl = $"{appUrl}/verify-email?token={Uri.EscapeDataString(verificationToken)}";
            var fromEmail = _configuration["Email:FromAddress"] ?? "onboarding@resend.dev";

            var message = new EmailMessage
            {
                From = fromEmail,
                To = new[] { toEmail },
                Subject = "Verify your SkillsBarter account",
                TextBody =
                    $@"Welcome to SkillsBarter, {userName}! Thank you for registering.

                    Please verify your email address by clicking the link below:
                    {verificationUrl}
                    This link will expire in 24 hours. If you didn't create an account, please ignore this email."
            };

            await _resend.EmailSendAsync(message);
            _logger.LogInformation($"Verification email sent to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send verification email to {toEmail}");
            throw;
        }
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetToken)
    {
        try
        {
            var appUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
            var resetUrl = $"{appUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";
            var fromEmail = _configuration["Email:FromAddress"] ?? "onboarding@resend.dev";

            var message = new EmailMessage
            {
                From = fromEmail,
                To = new[] { toEmail },
                Subject = "Reset your SkillsBarter password",
                TextBody = $@"Hello {userName}, We received a request to reset your password for your SkillsBarter account. Click the link below to reset your password: {resetUrl}
                This link will expire in 1 hour. If you didn't request a password reset, please ignore this email and your password will remain unchanged.
                For security reasons, we recommend that you:
                - Use a strong, unique password"
            };

            await _resend.EmailSendAsync(message);
            _logger.LogInformation($"Password reset email sent to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send password reset email to {toEmail}");
            throw;
        }
    }
}
