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
        
        var redisMock = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var dbMock = new Mock<StackExchange.Redis.IDatabase>();
        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        dbMock.Setup(x => x.StringSetAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<StackExchange.Redis.RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<StackExchange.Redis.When>(), It.IsAny<StackExchange.Redis.CommandFlags>())).ReturnsAsync(true);

        var handler = new CreateOrderCommandHandler(dbContextMock.Object, publishEndpointMock.Object, redisMock.Object);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);

        publishEndpointMock.Verify(x => x.Publish(It.IsAny<OrderCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
