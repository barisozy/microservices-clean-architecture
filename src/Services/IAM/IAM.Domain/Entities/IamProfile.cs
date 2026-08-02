namespace IAM.Domain.Entities;

public enum IamProfileStatus
{
    PendingIdentity,
    PendingActivation,
    Active,
    Suspended,
    Deactivated
}

public class IamProfile
{
    public Guid KeycloakSubject { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "CUSTOMER";
    public IamProfileStatus Status { get; set; } = IamProfileStatus.PendingIdentity;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
