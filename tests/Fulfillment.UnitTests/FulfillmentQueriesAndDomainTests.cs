using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Fulfillment.Application.Common.Interfaces;
using Fulfillment.Application.Fulfillment.Queries;
using Fulfillment.Domain.Entities;
using Fulfillment.Domain.Events;
using Fulfillment.Infrastructure;
using Microsoft.AspNetCore.Http;
using Moq;
using Shouldly;
using Xunit;

namespace Fulfillment.UnitTests;

public class FulfillmentQueriesAndDomainTests
{
    [Fact]
    public async Task GetFulfillmentTaskQueryHandler_ShouldReturnStatusFromRepository()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var repoMock = new Mock<IFulfillmentReadRepository>();
        repoMock.Setup(x => x.GetFulfillmentStatusAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Processing");

        var handler = new GetFulfillmentTaskQueryHandler(repoMock.Object);

        // Act
        var result = await handler.Handle(new GetFulfillmentTaskQuery(orderId), CancellationToken.None);

        // Assert
        result.ShouldBe("Processing");
        repoMock.Verify(x => x.GetFulfillmentStatusAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OrderShippedDomainEvent_ShouldStoreTaskProperty()
    {
        var task = new FulfillmentTask { OrderId = Guid.NewGuid(), Status = "Shipped", TrackingNumber = "TRK123" };
        var evt = new OrderShippedDomainEvent(task);

        evt.Task.ShouldBe(task);
        evt.Task.Status.ShouldBe("Shipped");
        evt.Task.TrackingNumber.ShouldBe("TRK123");
    }

    [Fact]
    public void CurrentUser_ShouldReturnClaimOrNull()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "usr-999") }))
            }
        };

        var currentUser = new CurrentUser(accessor);
        currentUser.Id.ShouldBe("usr-999");

        accessor.HttpContext = null;
        currentUser.Id.ShouldBeNull();
    }
}
