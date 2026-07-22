namespace ECommerce.Contracts.Events.v1;

public record OrderCancelled(
    Guid OrderId,
    string Reason,
    DateTimeOffset CancelledAt
);
