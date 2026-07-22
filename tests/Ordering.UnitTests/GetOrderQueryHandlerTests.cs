using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Ordering.Application.Common.Interfaces;
using Ordering.Application.Orders.Queries;
using Shouldly;
using Xunit;

namespace Ordering.UnitTests;

public class GetOrderQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnOrder_WhenOrderExists()
    {
        var readRepositoryMock = new Mock<IOrderReadRepository>();
        var orderId = Guid.NewGuid();
        var expectedDto = new OrderStatusDto(orderId, "Pending", "buyer-123");

        readRepositoryMock.Setup(x => x.GetOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var handler = new GetOrderQueryHandler(readRepositoryMock.Object);
        var result = await handler.Handle(new GetOrderQuery(orderId), CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(orderId);
        result.Status.ShouldBe("Pending");
        result.BuyerId.ShouldBe("buyer-123");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenOrderDoesNotExist()
    {
        var readRepositoryMock = new Mock<IOrderReadRepository>();
        var orderId = Guid.NewGuid();

        readRepositoryMock.Setup(x => x.GetOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderStatusDto?)null);

        var handler = new GetOrderQueryHandler(readRepositoryMock.Object);
        var result = await handler.Handle(new GetOrderQuery(orderId), CancellationToken.None);

        result.ShouldBeNull();
    }
}
