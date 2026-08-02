using System;
using Order.Domain.Events;
using Order.Domain.Exceptions;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class OrderDomainCoverageTests
{
    [Fact]
    public void OrderCancelledDomainEvent_ShouldStoreProperties()
    {
        var order = new global::Order.Domain.Entities.Order { Id = Guid.NewGuid(), BuyerId = "b-1" };
        var evt = new OrderCancelledDomainEvent(order, "Reason X");

        evt.Order.ShouldBe(order);
        evt.Reason.ShouldBe("Reason X");
    }

    [Fact]
    public void OrderDomainException_Constructors_ShouldWorkAsExpected()
    {
        var ex1 = new OrderDomainException();
        ex1.ShouldNotBeNull();

        var ex2 = new OrderDomainException("Custom error");
        ex2.Message.ShouldBe("Custom error");

        var inner = new InvalidOperationException("Inner error");
        var ex3 = new OrderDomainException("Wrapped error", inner);
        ex3.Message.ShouldBe("Wrapped error");
        ex3.InnerException.ShouldBe(inner);
    }
}
