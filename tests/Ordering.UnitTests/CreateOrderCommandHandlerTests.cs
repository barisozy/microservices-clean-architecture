using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Ordering.Application.Common.Interfaces;
using Ordering.Application.Orders.Commands.CreateOrder;
using Ordering.Domain.Entities;
using Shouldly;
using Xunit;

namespace Ordering.UnitTests;

public class CreateOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_Create_Order_And_Publish_Event()
    {
        // Arrange
        var contextMock = new Mock<IOrderingDbContext>();
        var publishMock = new Mock<IPublishEndpoint>();

        var dbSetMock = new Mock<DbSet<Order>>();
        contextMock.Setup(c => c.Orders).Returns(dbSetMock.Object);

        var handler = new CreateOrderCommandHandler(contextMock.Object, publishMock.Object);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto>
        {
            new OrderItemDto("SKU-1", 2, 100)
        });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBe(Guid.Empty);
        dbSetMock.Verify(d => d.Add(It.IsAny<Order>()), Times.Once);
        contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publishMock.Verify(p => p.Publish(It.IsAny<ECommerce.Contracts.Events.OrderCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
