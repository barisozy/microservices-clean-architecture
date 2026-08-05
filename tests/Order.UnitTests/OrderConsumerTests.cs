using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Moq;
using Order.Application.Common.Interfaces;
using Order.Application.Consumers;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class OrderConsumerTests
{
    [Fact]
    public async Task OrderCancelledConsumer_OrderNotFound_ShouldReturn()
    {
        var dbMock = new Mock<IOrderWriteRepository>();
        dbMock.Setup(x => x.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((global::Order.Domain.Entities.Order?)null);
            
        var contextMock = new Mock<ConsumeContext<OrderCancelled>>();
        contextMock.Setup(x => x.Message).Returns(new OrderCancelled(Guid.NewGuid(), "reason", DateTimeOffset.UtcNow));
        
        var consumer = new OrderCancelledConsumer(dbMock.Object);
        await consumer.Consume(contextMock.Object);
        
        dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task OrderCancelledConsumer_OrderExists_ShouldCancelAndSave()
    {
        var dbMock = new Mock<IOrderWriteRepository>();
        var order = global::Order.Domain.Entities.Order.Create("buyer", "key", new List<OrderItem>());
        dbMock.Setup(x => x.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
            
        var contextMock = new Mock<ConsumeContext<OrderCancelled>>();
        contextMock.Setup(x => x.Message).Returns(new OrderCancelled(Guid.NewGuid(), "reason", DateTimeOffset.UtcNow));
        
        var consumer = new OrderCancelledConsumer(dbMock.Object);
        await consumer.Consume(contextMock.Object);
        
        order.Status.ShouldBe(OrderStatus.Cancelled);
        dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task OrderInventoryConfirmedConsumer_OrderNotFound_ShouldThrow()
    {
        var dbMock = new Mock<IOrderWriteRepository>();
        dbMock.Setup(x => x.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((global::Order.Domain.Entities.Order?)null);
            
        var contextMock = new Mock<ConsumeContext<OrderInventoryConfirmed>>();
        contextMock.Setup(x => x.Message).Returns(new OrderInventoryConfirmed(Guid.NewGuid()));
        
        var consumer = new OrderInventoryConfirmedConsumer(dbMock.Object);
        
        await Should.ThrowAsync<InvalidOperationException>(() => consumer.Consume(contextMock.Object));
    }
    
    [Fact]
    public async Task OrderInventoryConfirmedConsumer_OrderExists_ShouldConfirmAndSave()
    {
        var dbMock = new Mock<IOrderWriteRepository>();
        var order = global::Order.Domain.Entities.Order.Create("buyer", "key", new List<OrderItem>());
        dbMock.Setup(x => x.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
            
        var contextMock = new Mock<ConsumeContext<OrderInventoryConfirmed>>();
        contextMock.Setup(x => x.Message).Returns(new OrderInventoryConfirmed(Guid.NewGuid()));
        
        var consumer = new OrderInventoryConfirmedConsumer(dbMock.Object);
        await consumer.Consume(contextMock.Object);
        
        order.Status.ShouldBe(OrderStatus.AwaitingPayment);
        dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
