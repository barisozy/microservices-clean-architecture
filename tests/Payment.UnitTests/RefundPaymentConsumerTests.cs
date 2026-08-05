using System;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.Consumers;
using Xunit;

namespace Payment.UnitTests;

public class RefundPaymentConsumerTests
{
    [Fact]
    public async Task Consume_LogsInformation()
    {
        var loggerMock = new Mock<ILogger<RefundPaymentConsumer>>();
        var contextMock = new Mock<ConsumeContext<RefundPayment>>();
        contextMock.Setup(x => x.Message).Returns(new RefundPayment(Guid.NewGuid(), "reason"));

        var consumer = new RefundPaymentConsumer(loggerMock.Object);
        await consumer.Consume(contextMock.Object);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Payment refund processed")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
