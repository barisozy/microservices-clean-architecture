using MassTransit;

namespace Order.Application.Checkout;

public sealed class CheckoutState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public decimal TotalAmount { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? InventoryReservedAt { get; set; }
    public Guid? ReservationId { get; set; }
    public string? FailureReason { get; set; }
    public uint Version { get; set; }
    public Guid? TimeoutTokenId { get; set; }
}
