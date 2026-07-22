using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Consumers;
using Inventory.Application.Inventory.Commands;
using Inventory.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore;
using Xunit;

namespace Inventory.UnitTests;

public class PaymentFailedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldSendReleaseStockCommand_WhenReservationExists()
    {
        var senderMock = new Mock<ISender>();
        var dbContextMock = new Mock<IInventoryDbContext>();
        var loggerMock = new Mock<ILogger<PaymentFailedConsumer>>();

        var orderId = Guid.NewGuid();
        var reservation = InventoryReservation.Create(orderId, "SKU-1", 5);
        var reservations = new List<InventoryReservation> { reservation };

        dbContextMock.Setup(x => x.Reservations).ReturnsDbSet(reservations);

        var consumer = new PaymentFailedConsumer(senderMock.Object, dbContextMock.Object, loggerMock.Object);

        var message = new PaymentFailed(orderId, "customer-1", "Payment declined", DateTimeOffset.UtcNow);
        var contextMock = new Mock<ConsumeContext<PaymentFailed>>();
        contextMock.Setup(x => x.Message).Returns(message);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        senderMock.Verify(x => x.Send(It.Is<ReleaseStockCommand>(cmd => cmd.ReservationId == reservation.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldNotSendReleaseStockCommand_WhenReservationDoesNotExist()
    {
        var senderMock = new Mock<ISender>();
        var dbContextMock = new Mock<IInventoryDbContext>();
        var loggerMock = new Mock<ILogger<PaymentFailedConsumer>>();

        var reservations = new List<InventoryReservation>();
        dbContextMock.Setup(x => x.Reservations).ReturnsDbSet(reservations);

        var consumer = new PaymentFailedConsumer(senderMock.Object, dbContextMock.Object, loggerMock.Object);

        var orderId = Guid.NewGuid();
        var message = new PaymentFailed(orderId, "customer-1", "Payment declined", DateTimeOffset.UtcNow);
        var contextMock = new Mock<ConsumeContext<PaymentFailed>>();
        contextMock.Setup(x => x.Message).Returns(message);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        senderMock.Verify(x => x.Send(It.IsAny<ReleaseStockCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
