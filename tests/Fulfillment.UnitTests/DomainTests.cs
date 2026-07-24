using System;
using Fulfillment.Domain.Common;
using Fulfillment.Domain.Entities;
using Shouldly;
using Xunit;

namespace Fulfillment.UnitTests;

public class TestDomainEvent : BaseEvent
{
    public string Message { get; set; } = "Test";
}

public class DomainTests
{
    [Fact]
    public void BaseEntity_DomainEvents_Management_ShouldWorkCorrectly()
    {
        // Arrange
        var task = new FulfillmentTask();
        var domainEvent = new TestDomainEvent();

        // Act & Assert AddDomainEvent
        task.AddDomainEvent(domainEvent);
        task.DomainEvents.Count.ShouldBe(1);
        task.DomainEvents.ShouldContain(domainEvent);

        // Act & Assert RemoveDomainEvent
        task.RemoveDomainEvent(domainEvent);
        task.DomainEvents.Count.ShouldBe(0);

        // Act & Assert ClearDomainEvents
        task.AddDomainEvent(domainEvent);
        task.ClearDomainEvents();
        task.DomainEvents.Count.ShouldBe(0);
    }

    [Fact]
    public void BaseAuditableEntity_Properties_ShouldBeSetAndGetCorrectly()
    {
        // Arrange & Act
        var now = DateTimeOffset.UtcNow;
        var task = new FulfillmentTask
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Status = "Shipped",
            TrackingNumber = "TRACK-123",
            CreatedAt = now,
            CreatedBy = "user-1",
            LastModifiedAt = now,
            LastModifiedBy = "user-2"
        };

        // Assert
        task.Status.ShouldBe("Shipped");
        task.TrackingNumber.ShouldBe("TRACK-123");
        task.CreatedAt.ShouldBe(now);
        task.CreatedBy.ShouldBe("user-1");
        task.LastModifiedAt.ShouldBe(now);
        task.LastModifiedBy.ShouldBe("user-2");
    }

    [Fact]
    public void BaseEvent_DateOccurred_ShouldDefaultToUtcNow()
    {
        // Arrange & Act
        var evt = new TestDomainEvent();

        // Assert
        evt.DateOccurred.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}
