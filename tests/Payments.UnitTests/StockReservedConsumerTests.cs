using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Payments.Application.Consumers;
using Shouldly;
using Xunit;

namespace Payments.UnitTests;

public class StockReservedConsumerTests
{
    [Fact]
    public async Task Consume_Should_Log_And_Process()
    {
        var senderMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<StockReservedConsumer>>();
        var consumer = new StockReservedConsumer(senderMock.Object, loggerMock.Object);

        var consumeContextMock = new Mock<ConsumeContext<StockReserved>>();
        consumeContextMock.Setup(x => x.Message).Returns(new StockReserved(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemContractDto>(), 100, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        await consumer.Consume(consumeContextMock.Object);
        // Should not throw
    }
}
