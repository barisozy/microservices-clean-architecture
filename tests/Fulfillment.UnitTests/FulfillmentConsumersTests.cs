using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using Fulfillment.Application.Common.Interfaces;
using Fulfillment.Application.Consumers;
using Fulfillment.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Fulfillment.UnitTests;

public class FulfillmentConsumersTests
{
    [Fact]
    public async Task PaymentCompletedConsumer_Should_Create_Task_And_Publish()
    {
        var contextMock = new Mock<IFulfillmentDbContext>();
        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<PaymentCompletedConsumer>>();

        var tasksList = new List<FulfillmentTask>();
        contextMock.Setup(x => x.Tasks).ReturnsDbSet(tasksList);
        contextMock.Setup(x => x.Tasks.Add(It.IsAny<FulfillmentTask>())).Callback<FulfillmentTask>(t => tasksList.Add(t));

        var readRepoMock = new Mock<IFulfillmentReadRepository>();

        var consumer = new PaymentCompletedConsumer(contextMock.Object, publishMock.Object, loggerMock.Object, readRepoMock.Object);

        var consumeContextMock = new Mock<ConsumeContext<PaymentCompleted>>();
        consumeContextMock.Setup(x => x.Message).Returns(new PaymentCompleted(Guid.NewGuid(), Guid.NewGuid(), "key1", DateTimeOffset.UtcNow));
        consumeContextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(consumeContextMock.Object);

        tasksList.ShouldNotBeEmpty();
        tasksList[0].Status.ShouldBe("Shipped");
        tasksList[0].TrackingNumber.ShouldNotBeNullOrEmpty();
        
        contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publishMock.Verify(x => x.Publish(It.IsAny<OrderShipped>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
