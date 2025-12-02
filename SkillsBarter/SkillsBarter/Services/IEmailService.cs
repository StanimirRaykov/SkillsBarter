namespace SkillsBarter.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string userName, string verificationToken);
}
