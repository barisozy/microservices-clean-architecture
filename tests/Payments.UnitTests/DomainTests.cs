using Payments.Domain.Common;
using Payments.Domain.Events;
using Payments.Domain.Entities;
using System;
using Shouldly;
using Xunit;

namespace Payments.UnitTests;

public class DomainTests
{
    private class TestEntity : BaseEntity { }

    [Fact]
    public void BaseEntity_Should_Manage_Events()
    {
        var entity = new TestEntity();
        var payment = PaymentRecord.Create(Guid.NewGuid(), "key", 10m);
        var domainEvent = new PaymentCompletedDomainEvent(payment);

        entity.AddDomainEvent(domainEvent);
        entity.DomainEvents.ShouldContain(domainEvent);

        entity.RemoveDomainEvent(domainEvent);
        entity.DomainEvents.ShouldNotContain(domainEvent);

        entity.AddDomainEvent(domainEvent);
        entity.ClearDomainEvents();
        entity.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void PaymentCompletedDomainEvent_Should_Store_Values()
    {
        var payment = PaymentRecord.Create(Guid.NewGuid(), "key", 10m);
        var evt = new PaymentCompletedDomainEvent(payment);
        evt.Payment.ShouldBe(payment);
    }

    [Fact]
    public void PaymentFailedDomainEvent_Should_Store_Values()
    {
        var payment = PaymentRecord.Create(Guid.NewGuid(), "key", 10m);
        var evt = new PaymentFailedDomainEvent(payment, "reason");
        evt.Payment.ShouldBe(payment);
        evt.Reason.ShouldBe("reason");
    }
}
