namespace IAM.Domain.Entities;

public class Invitation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "CUSTOMER";
    public string Status { get; set; } = "PENDING";
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public Guid IdempotencyKey { get; set; }
}
