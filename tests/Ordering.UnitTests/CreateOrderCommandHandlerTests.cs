using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using ECommerce.Contracts.Events.v1;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Ordering.Application.Common.Interfaces;
using Ordering.Application.Orders.Commands.CreateOrder;
using Ordering.Domain.Entities;
using Xunit;

namespace Ordering.UnitTests;

public class CreateOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPublishOrderCreatedEvent()
    {
        var dbContextMock = new Mock<IOrderingDbContext>();
        var dbSetMock = new Mock<DbSet<Order>>();
        dbContextMock.Setup(x => x.Orders).Returns(dbSetMock.Object);

        var publishEndpointMock = new Mock<IPublishEndpoint>();

        var handler = new CreateOrderCommandHandler(dbContextMock.Object, publishEndpointMock.Object);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);

        publishEndpointMock.Verify(x => x.Publish(It.IsAny<OrderCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
