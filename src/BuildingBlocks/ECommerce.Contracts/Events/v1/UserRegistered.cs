namespace ECommerce.Contracts.Events.v1;

public record UserRegistered(Guid KeycloakSubject, string Email);
