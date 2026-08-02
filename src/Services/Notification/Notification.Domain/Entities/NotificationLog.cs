namespace Notification.Domain.Entities;

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string EventType { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
