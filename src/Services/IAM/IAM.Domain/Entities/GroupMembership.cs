namespace IAM.Domain.Entities;

public class GroupMembership
{
    public Guid KeycloakSubject { get; set; }
    public string GroupId { get; set; } = string.Empty;
}
