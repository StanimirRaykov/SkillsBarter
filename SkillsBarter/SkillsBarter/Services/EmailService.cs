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
            var appUrl = _configuration["AppUrl"] ?? "http://localhost:5000";
            var verificationUrl = $"{appUrl}/verify-email?token={verificationToken}";
            var fromEmail = _configuration["Email:FromAddress"] ?? "onboarding@resend.dev";

            var message = new EmailMessage
            {
                From = fromEmail,
                To = new[] { toEmail },
                Subject = "Verify your SkillsBarter account",
                TextBody = $@"Welcome to SkillsBarter, {userName}! Thank you for registering. Please verify your email address by clicking the link below:
                {verificationUrl}.This link will expire in 24 hours. If you didn't create an account, please ignore this email."
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
}
