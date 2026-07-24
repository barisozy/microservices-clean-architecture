using System;
using Payments.Domain.Common;
using Payments.Domain.Entities;
using Payments.Domain.Events;
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
    public void BaseAuditableEntity_Properties_ShouldBeSetAndGet()
    {
        var now = DateTimeOffset.UtcNow;
        var payment = PaymentRecord.Create(Guid.NewGuid(), "key-123", 99.99m);
        payment.CreatedAt = now;
        payment.CreatedBy = "user";
        payment.LastModifiedAt = now;
        payment.LastModifiedBy = "user2";

        payment.IdempotencyKey.ShouldBe("key-123");
        payment.Amount.ShouldBe(99.99m);
        payment.CreatedAt.ShouldBe(now);
        payment.CreatedBy.ShouldBe("user");
        payment.LastModifiedAt.ShouldBe(now);
        payment.LastModifiedBy.ShouldBe("user2");
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
