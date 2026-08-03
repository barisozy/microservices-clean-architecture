namespace Customer.Domain.Entities;

public sealed class CustomerProfile
{
    public Guid KeycloakSubject { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
