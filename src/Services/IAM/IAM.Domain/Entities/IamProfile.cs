namespace IAM.Domain.Entities;

public sealed class IamProfile
{
    public Guid KeycloakSubject { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "CUSTOMER";
    public IamProfileStatus Status { get; set; } = IamProfileStatus.PendingIdentity;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
