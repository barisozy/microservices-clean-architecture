using Fulfillment.Domain.Entities;
using Shouldly;
using Xunit;
using System;

namespace Fulfillment.UnitTests;

public class FulfillmentTests
{
    [Fact]
    public void FulfillmentTask_Creation_Should_Have_Default_Status_Pending()
    {
        var task = new FulfillmentTask { OrderId = Guid.NewGuid() };
        task.Status.ShouldBe("Pending");
        task.TrackingNumber.ShouldBeEmpty();
    }
}
