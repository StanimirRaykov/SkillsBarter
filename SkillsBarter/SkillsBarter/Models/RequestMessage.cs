namespace SkillsBarter.Models;

public class RequestMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public virtual RequestThread Thread { get; set; } = null!;
    public virtual ApplicationUser Sender { get; set; } = null!;
}
