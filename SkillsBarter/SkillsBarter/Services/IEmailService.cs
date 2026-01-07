namespace SkillsBarter.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string userName, string verificationToken);
    Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetToken);
    Task SendEmailChangeConfirmationAsync(string toEmail, string userName, string confirmationToken);
}
