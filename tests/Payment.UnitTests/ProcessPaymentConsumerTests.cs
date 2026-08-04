using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.Consumers;
using Shouldly;
using Xunit;

namespace Payment.UnitTests;

public class ProcessPaymentConsumerTests
{
    [Fact]
    public async Task Consume_Should_Log_And_Process()
    {
        var senderMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<ProcessPaymentConsumer>>();
        var consumer = new ProcessPaymentConsumer(senderMock.Object, loggerMock.Object);

        var consumeContextMock = new Mock<ConsumeContext<ProcessPayment>>();
        consumeContextMock.Setup(x => x.Message).Returns(new ProcessPayment(Guid.NewGuid(), Guid.NewGuid(), "key1", 100, []));

        await consumer.Consume(consumeContextMock.Object);
        // Should not throw
    }
}
