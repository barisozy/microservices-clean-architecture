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

    public Schedule<CheckoutState, ReservationTimeout> ReservationTimeoutSchedule { get; private set; } = null!;
    public Schedule<CheckoutState, PaymentTimeout> PaymentTimeoutSchedule { get; private set; } = null!;
    public Schedule<CheckoutState, InventoryCommitTimeout> InventoryCommitTimeoutSchedule { get; private set; } = null!;


    private readonly bool _isTest;

    public CheckoutStateMachine(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _isTest = string.Equals(configuration["DOTNET_ENVIRONMENT"], "IntegrationTesting", StringComparison.OrdinalIgnoreCase);
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

        Schedule(() => ReservationTimeoutSchedule, instance => instance.TimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromMinutes(2);
            s.Received = r => r.CorrelateById(context => context.Message.OrderId);
        });

        Schedule(() => PaymentTimeoutSchedule, instance => instance.TimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromMinutes(15);
            s.Received = r => r.CorrelateById(context => context.Message.OrderId);
        });

        Schedule(() => InventoryCommitTimeoutSchedule, instance => instance.TimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromMinutes(2);
            s.Received = r => r.CorrelateById(context => context.Message.OrderId);
        });
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
                .If(ctx => !_isTest, x => x.Schedule(ReservationTimeoutSchedule, ctx => new ReservationTimeout(ctx.Message.OrderId)))
                .Publish(ctx => new ReserveInventory(ctx.Message.OrderId, ctx.Message.Items))
                .TransitionTo(ReservingInventory));

        During(ReservingInventory,
            When(InventoryReserved)
                .If(ctx => !_isTest, x => x.Unschedule(ReservationTimeoutSchedule))
                .Then(ctx =>
                {
                    ctx.Saga.ReservationId = ctx.Message.ReservationId;
                    ctx.Saga.InventoryReservedAt = DateTimeOffset.UtcNow;
                })
                .If(ctx => !_isTest, x => x.Schedule(PaymentTimeoutSchedule, ctx => new PaymentTimeout(ctx.Message.OrderId)))
                .Publish(ctx => new ProcessPayment(ctx.Saga.CorrelationId, ctx.Saga.CustomerId, ctx.Saga.IdempotencyKey, ctx.Saga.TotalAmount, JsonSerializer.Deserialize<List<OrderItemContractDto>>(ctx.Saga.ItemsJson!)!))
                .TransitionTo(AwaitingPayment),
            When(InventoryRejected)
                .If(ctx => !_isTest, x => x.Unschedule(ReservationTimeoutSchedule))
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new OrderCancelled(ctx.Message.OrderId, ctx.Message.Reason, DateTimeOffset.UtcNow))
                .Finalize(),
            When(InventoryExpired)
                .If(ctx => !_isTest, x => x.Unschedule(ReservationTimeoutSchedule))
                .Then(ctx => ctx.Saga.FailureReason = "RESERVATION_EXPIRED")
                .Publish(ctx => new OrderCancelled(ctx.Message.OrderId, "RESERVATION_EXPIRED", DateTimeOffset.UtcNow))
                .Finalize(),
            When(OrderCancelled)
                .If(ctx => !_isTest, x => x.Unschedule(ReservationTimeoutSchedule))
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Message.OrderId, ctx.Message.Reason))
                .Finalize(),
            When(ReservationTimeoutSchedule.Received)
                .Then(ctx => ctx.Saga.FailureReason = "RESERVATION_TIMEOUT")
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, "RESERVATION_TIMEOUT"))
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, "RESERVATION_TIMEOUT", DateTimeOffset.UtcNow))
                .Finalize());

        During(AwaitingPayment,
            When(PaymentCompleted)
                .If(ctx => !_isTest, x => x.Unschedule(PaymentTimeoutSchedule))
                .If(ctx => !_isTest, x => x.Schedule(InventoryCommitTimeoutSchedule, ctx => new InventoryCommitTimeout(ctx.Saga.CorrelationId)))
                .Publish(ctx => new CommitInventoryReservation(ctx.Saga.CorrelationId))
                .TransitionTo(ConfirmingInventory),
            When(PaymentFailed)
                .If(ctx => !_isTest, x => x.Unschedule(PaymentTimeoutSchedule))
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, ctx.Message.Reason))
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, ctx.Message.Reason, DateTimeOffset.UtcNow))
                .Finalize(),
            When(OrderCancelled)
                .If(ctx => !_isTest, x => x.Unschedule(PaymentTimeoutSchedule))
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, ctx.Message.Reason))
                .Publish(ctx => new RefundPayment(ctx.Saga.CorrelationId, ctx.Message.Reason))
                .Finalize(),
            When(InventoryExpired)
                .If(ctx => !_isTest, x => x.Unschedule(PaymentTimeoutSchedule))
                .Then(ctx => ctx.Saga.FailureReason = "RESERVATION_EXPIRED")
                .Publish(ctx => new RefundPayment(ctx.Saga.CorrelationId, "RESERVATION_EXPIRED"))
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, "RESERVATION_EXPIRED", DateTimeOffset.UtcNow))
                .Finalize(),
            When(PaymentTimeoutSchedule.Received)
                .Then(ctx => ctx.Saga.FailureReason = "PAYMENT_TIMEOUT")
                .Publish(ctx => new RefundPayment(ctx.Saga.CorrelationId, "PAYMENT_TIMEOUT"))
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, "PAYMENT_TIMEOUT"))
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, "PAYMENT_TIMEOUT", DateTimeOffset.UtcNow))
                .Finalize());

        During(ConfirmingInventory,
            When(InventoryCommitted)
                .If(ctx => !_isTest, x => x.Unschedule(InventoryCommitTimeoutSchedule))
                .Publish(ctx => new OrderInventoryConfirmed(ctx.Message.OrderId))
                .Publish(ctx => new OrderCheckoutCompleted(
                    ctx.Message.OrderId,
                    ctx.Saga.CustomerId,
                    ctx.Saga.IdempotencyKey,
                    JsonSerializer.Deserialize<List<OrderItemContractDto>>(ctx.Saga.ItemsJson) ?? new List<OrderItemContractDto>(),
                    ctx.Saga.TotalAmount,
                    DateTimeOffset.UtcNow))
                .Finalize(),
            When(InventoryCommitFailed)
                .If(ctx => !_isTest, x => x.Unschedule(InventoryCommitTimeoutSchedule))
                .Then(ctx => ctx.Saga.FailureReason = "INVENTORY_COMMIT_FAILED")
                .Publish(ctx => new RefundPayment(ctx.Saga.CorrelationId, "INVENTORY_COMMIT_FAILED"))
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, "INVENTORY_COMMIT_FAILED"))
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, "INVENTORY_COMMIT_FAILED", DateTimeOffset.UtcNow))
                .Finalize(),
            When(InventoryRejected)
                .If(ctx => !_isTest, x => x.Unschedule(InventoryCommitTimeoutSchedule))
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new RefundPayment(ctx.Saga.CorrelationId, ctx.Message.Reason))
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, ctx.Message.Reason))
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, ctx.Message.Reason, DateTimeOffset.UtcNow))
                .Finalize(),
            When(InventoryCommitTimeoutSchedule.Received)
                .Then(ctx => ctx.Saga.FailureReason = "INVENTORY_COMMIT_TIMEOUT")
                .Publish(ctx => new RefundPayment(ctx.Saga.CorrelationId, "INVENTORY_COMMIT_TIMEOUT"))
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.CorrelationId, "INVENTORY_COMMIT_TIMEOUT"))
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, "INVENTORY_COMMIT_TIMEOUT", DateTimeOffset.UtcNow))
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
