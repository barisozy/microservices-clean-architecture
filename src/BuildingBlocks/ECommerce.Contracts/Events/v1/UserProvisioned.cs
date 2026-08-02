namespace ECommerce.Contracts.Events.v1;

public sealed record UserProvisioned(
    Guid KeycloakSubject,
    string Email,
    DateTimeOffset ProvisionedAt);
