using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.EventHandlers;
using Order.Domain.Common;
using Order.Domain.Entities;
using Order.Domain.Events;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class DomainEventDispatcherTests
{
    private class DummyEvent : BaseEvent { }

    [Fact]
    public async Task Dispatch_OrderCreatedDomainEvent_InvokesHandlers()
    {
        var handlerMock = new Mock<IDomainEventHandler<OrderCreatedDomainEvent>>();
        var dispatcher = new DomainEventDispatcher(
            new[] { handlerMock.Object },
            Array.Empty<IDomainEventHandler<OrderInventoryConfirmedDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderCancelledDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderPaidDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderShippedDomainEvent>>()
        );

        var order = global::Order.Domain.Entities.Order.Create("buyer", "key", new List<OrderItem>());
        var evt = new OrderCreatedDomainEvent(order);
        
        await dispatcher.Dispatch(evt, CancellationToken.None);
        handlerMock.Verify(h => h.Handle(evt, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Dispatch_OrderInventoryConfirmedDomainEvent_InvokesHandlers()
    {
        var handlerMock = new Mock<IDomainEventHandler<OrderInventoryConfirmedDomainEvent>>();
        var dispatcher = new DomainEventDispatcher(
            Array.Empty<IDomainEventHandler<OrderCreatedDomainEvent>>(),
            new[] { handlerMock.Object },
            Array.Empty<IDomainEventHandler<OrderCancelledDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderPaidDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderShippedDomainEvent>>()
        );

        var order = global::Order.Domain.Entities.Order.Create("buyer", "key", new List<OrderItem>());
        var evt = new OrderInventoryConfirmedDomainEvent(order);
        
        await dispatcher.Dispatch(evt, CancellationToken.None);
        handlerMock.Verify(h => h.Handle(evt, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Dispatch_OrderCancelledDomainEvent_InvokesHandlers()
    {
        var handlerMock = new Mock<IDomainEventHandler<OrderCancelledDomainEvent>>();
        var dispatcher = new DomainEventDispatcher(
            Array.Empty<IDomainEventHandler<OrderCreatedDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderInventoryConfirmedDomainEvent>>(),
            new[] { handlerMock.Object },
            Array.Empty<IDomainEventHandler<OrderPaidDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderShippedDomainEvent>>()
        );

        var order = global::Order.Domain.Entities.Order.Create("buyer", "key", new List<OrderItem>());
        var evt = new OrderCancelledDomainEvent(order, "reason");
        
        await dispatcher.Dispatch(evt, CancellationToken.None);
        handlerMock.Verify(h => h.Handle(evt, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Dispatch_OrderPaidDomainEvent_InvokesHandlers()
    {
        var handlerMock = new Mock<IDomainEventHandler<OrderPaidDomainEvent>>();
        var dispatcher = new DomainEventDispatcher(
            Array.Empty<IDomainEventHandler<OrderCreatedDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderInventoryConfirmedDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderCancelledDomainEvent>>(),
            new[] { handlerMock.Object },
            Array.Empty<IDomainEventHandler<OrderShippedDomainEvent>>()
        );

        var order = global::Order.Domain.Entities.Order.Create("buyer", "key", new List<OrderItem>());
        var evt = new OrderPaidDomainEvent(order);
        
        await dispatcher.Dispatch(evt, CancellationToken.None);
        handlerMock.Verify(h => h.Handle(evt, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Dispatch_OrderShippedDomainEvent_InvokesHandlers()
    {
        var handlerMock = new Mock<IDomainEventHandler<OrderShippedDomainEvent>>();
        var dispatcher = new DomainEventDispatcher(
            Array.Empty<IDomainEventHandler<OrderCreatedDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderInventoryConfirmedDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderCancelledDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderPaidDomainEvent>>(),
            new[] { handlerMock.Object }
        );

        var order = global::Order.Domain.Entities.Order.Create("buyer", "key", new List<OrderItem>());
        var evt = new OrderShippedDomainEvent(order);
        
        await dispatcher.Dispatch(evt, CancellationToken.None);
        handlerMock.Verify(h => h.Handle(evt, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Dispatch_UnknownEvent_ThrowsInvalidOperationException()
    {
        var dispatcher = new DomainEventDispatcher(
            Array.Empty<IDomainEventHandler<OrderCreatedDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderInventoryConfirmedDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderCancelledDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderPaidDomainEvent>>(),
            Array.Empty<IDomainEventHandler<OrderShippedDomainEvent>>()
        );

        await Should.ThrowAsync<InvalidOperationException>(() =>
            dispatcher.Dispatch(new DummyEvent(), CancellationToken.None));
    }
}
