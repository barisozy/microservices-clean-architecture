using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Payments.Application.Consumers;
using Shouldly;
using Xunit;

namespace Payments.UnitTests;

public class OrderCreatedConsumerTests
{
    [Fact]
    public async Task Consume_Should_Log_And_Process()
    {
        var senderMock = new Mock<ISender>();
        var consumer = new OrderCreatedConsumer(senderMock.Object);

        var consumeContextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        consumeContextMock.Setup(x => x.Message).Returns(new OrderCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemContractDto>(), 100, DateTimeOffset.UtcNow));

        await consumer.Consume(consumeContextMock.Object);
        // Should not throw
    }
}
