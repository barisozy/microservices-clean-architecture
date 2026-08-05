using ECommerce.Contracts.Events.v1;
using MassTransit;
using System.Text.Json;
using System.Collections.Generic;
using System;

namespace Order.Application.Checkout;

public sealed class CheckoutStateMachine : MassTransitStateMachine<CheckoutState>
{
    public State ReservingInventory { get; private set; } = null!;
    public State ConfirmingInventory { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public Event<OrderCheckoutStarted> CheckoutStarted { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReserved { get; private set; } = null!;
    public Event<InventoryReservationRejected> InventoryRejected { get; private set; } = null!;
    public Event<InventoryReservationCommitted> InventoryCommitted { get; private set; } = null!;
    public Event<InventoryReservationExpired> InventoryExpired { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;
    public Event<OrderCancelled> OrderCancelled { get; private set; } = null!;
    public Event<Fault<CommitInventoryReservation>> InventoryCommitFailed { get; private set; } = null!;

    public CheckoutStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => CheckoutStarted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => InventoryReserved, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => InventoryRejected, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => InventoryCommitted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => InventoryExpired, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentCompleted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentFailed, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderCancelled, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => InventoryCommitFailed, x => x.CorrelateById(context => context.Message.Message.OrderId));

        Initially(
            When(CheckoutStarted)
                .Then(ctx =>
                {
                    ctx.Saga.CustomerId = ctx.Message.CustomerId;
                    ctx.Saga.IdempotencyKey = ctx.Message.IdempotencyKey;
                    ctx.Saga.ItemsJson = JsonSerializer.Serialize(ctx.Message.Items);
                    ctx.Saga.TotalAmount = ctx.Message.TotalAmount;
                    ctx.Saga.StartedAt = ctx.Message.OccurredAt;
                })
                .Publish(ctx => new ReserveInventory(ctx.Message.OrderId, ctx.Message.Items))
                .TransitionTo(ReservingInventory));

        During(ReservingInventory,
            When(InventoryReserved)
                .Then(ctx =>
                {
                    ctx.Saga.ReservationId = ctx.Message.ReservationId;
                    ctx.Saga.InventoryReservedAt = DateTimeOffset.UtcNow;
                })
                .Publish(ctx => new ProcessPayment(ctx.Saga.CorrelationId, ctx.Saga.CustomerId, ctx.Saga.IdempotencyKey, ctx.Saga.TotalAmount, JsonSerializer.Deserialize<List<OrderItemContractDto>>(ctx.Saga.ItemsJson!)!))
                .TransitionTo(AwaitingPayment),
            When(InventoryRejected)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new OrderCancelled(ctx.Message.OrderId, ctx.Message.Reason, DateTimeOffset.UtcNow))
                .Finalize(),
            When(InventoryExpired)
                .Then(ctx => ctx.Saga.FailureReason = "RESERVATION_EXPIRED")
                .Publish(ctx => new OrderCancelled(ctx.Message.OrderId, "RESERVATION_EXPIRED", DateTimeOffset.UtcNow))
                .Finalize(),
            When(OrderCancelled)
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Message.OrderId, ctx.Message.Reason))
                .Finalize());

        During(AwaitingPayment,
            When(PaymentCompleted)
                .Publish(ctx => new CommitInventoryReservation(ctx.Saga.CorrelationId))
                .TransitionTo(ConfirmingInventory),
            When(PaymentFailed)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, ctx.Message.Reason))
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, ctx.Message.Reason, DateTimeOffset.UtcNow))
                .Finalize(),
            When(OrderCancelled)
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, ctx.Message.Reason))
                .Publish(ctx => new RefundPayment(ctx.Saga.CorrelationId, ctx.Message.Reason))
                .Finalize());

        During(ConfirmingInventory,
            When(InventoryCommitted)
                .Publish(ctx => new OrderInventoryConfirmed(ctx.Message.OrderId))
                .Publish(ctx => new OrderCreated(
                    ctx.Message.OrderId,
                    ctx.Saga.CustomerId,
                    ctx.Saga.IdempotencyKey,
                    JsonSerializer.Deserialize<List<OrderItemContractDto>>(ctx.Saga.ItemsJson) ?? new List<OrderItemContractDto>(),
                    ctx.Saga.TotalAmount,
                    DateTimeOffset.UtcNow))
                .Finalize(),
            When(InventoryCommitFailed)
                .Then(ctx => ctx.Saga.FailureReason = "INVENTORY_COMMIT_FAILED")
                .Publish(ctx => new RefundPayment(ctx.Saga.CorrelationId, "INVENTORY_COMMIT_FAILED"))
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, "INVENTORY_COMMIT_FAILED"))
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, "INVENTORY_COMMIT_FAILED", DateTimeOffset.UtcNow))
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
