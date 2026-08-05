namespace ECommerce.Contracts.Events.v1;

public record OrderConfirmed(Guid OrderId, DateTimeOffset ConfirmedAt);