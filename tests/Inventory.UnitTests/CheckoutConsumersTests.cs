using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Consumers;
using Inventory.Domain.Entities;
using MassTransit;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Inventory.UnitTests;

public class CheckoutConsumersTests
{
    private readonly Mock<IInventoryWriteRepository> _dbMock;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Mock<IInventoryReservationLeasePolicy> _leasePolicyMock;

    public CheckoutConsumersTests()
    {
        _dbMock = new Mock<IInventoryWriteRepository>();
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _leasePolicyMock = new Mock<IInventoryReservationLeasePolicy>();
    }

    [Fact]
    public async Task ReserveInventoryConsumer_EmptyItems_Rejects()
    {
        var consumer = new ReserveInventoryConsumer(_dbMock.Object, _publishEndpointMock.Object, _leasePolicyMock.Object);
        var contextMock = new Mock<ConsumeContext<ReserveInventory>>();
        contextMock.Setup(c => c.Message).Returns(new ReserveInventory(Guid.NewGuid(), new List<OrderItemContractDto>()));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        _publishEndpointMock.Verify(p => p.Publish(It.Is<InventoryReservationRejected>(r => r.Reason == "INVALID_RESERVATION_REQUEST"), It.IsAny<IPipe<PublishContext<InventoryReservationRejected>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveInventoryConsumer_UnknownSku_Rejects()
    {
        var consumer = new ReserveInventoryConsumer(_dbMock.Object, _publishEndpointMock.Object, _leasePolicyMock.Object);
        var contextMock = new Mock<ConsumeContext<ReserveInventory>>();
        var orderId = Guid.NewGuid();
        contextMock.Setup(c => c.Message).Returns(new ReserveInventory(orderId, new List<OrderItemContractDto> { new("SKU1", 1, 10.0m) }));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _dbMock.Setup(d => d.FindStockAsync("SKU1", It.IsAny<CancellationToken>())).ReturnsAsync((Stock?)null);

        await consumer.Consume(contextMock.Object);

        _publishEndpointMock.Verify(p => p.Publish(It.Is<InventoryReservationRejected>(r => r.Reason == "UNKNOWN_SKU"), It.IsAny<IPipe<PublishContext<InventoryReservationRejected>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveInventoryConsumer_InsufficientStock_Rejects()
    {
        var consumer = new ReserveInventoryConsumer(_dbMock.Object, _publishEndpointMock.Object, _leasePolicyMock.Object);
        var contextMock = new Mock<ConsumeContext<ReserveInventory>>();
        var orderId = Guid.NewGuid();
        contextMock.Setup(c => c.Message).Returns(new ReserveInventory(orderId, new List<OrderItemContractDto> { new("SKU1", 10, 10.0m) }));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        var stock = new Stock("SKU1", 5);
        _dbMock.Setup(d => d.FindStockAsync("SKU1", It.IsAny<CancellationToken>())).ReturnsAsync(stock);

        await consumer.Consume(contextMock.Object);

        _publishEndpointMock.Verify(p => p.Publish(It.Is<InventoryReservationRejected>(r => r.Reason == "INSUFFICIENT_STOCK"), It.IsAny<IPipe<PublishContext<InventoryReservationRejected>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveInventoryConsumer_Success_PublishesReserved()
    {
        var consumer = new ReserveInventoryConsumer(_dbMock.Object, _publishEndpointMock.Object, _leasePolicyMock.Object);
        var contextMock = new Mock<ConsumeContext<ReserveInventory>>();
        var orderId = Guid.NewGuid();
        contextMock.Setup(c => c.Message).Returns(new ReserveInventory(orderId, new List<OrderItemContractDto> { new("SKU1", 2, 10.0m) }));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        var stock = new Stock("SKU1", 10);
        _dbMock.Setup(d => d.FindStockAsync("SKU1", It.IsAny<CancellationToken>())).ReturnsAsync(stock);
        
        var now = DateTimeOffset.UtcNow;
        _leasePolicyMock.Setup(l => l.UtcNow).Returns(now);
        _leasePolicyMock.Setup(l => l.GetExpiry(now)).Returns(now.AddMinutes(5));

        await consumer.Consume(contextMock.Object);

        _dbMock.Verify(d => d.Add(It.IsAny<InventoryReservation>()), Times.Once);
        _publishEndpointMock.Verify(p => p.Publish(It.IsAny<InventoryReserved>(), It.IsAny<IPipe<PublishContext<InventoryReserved>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitInventoryReservationConsumer_NotFound_Rejects()
    {
        var consumer = new CommitInventoryReservationConsumer(_dbMock.Object, _publishEndpointMock.Object, _leasePolicyMock.Object);
        var contextMock = new Mock<ConsumeContext<CommitInventoryReservation>>();
        contextMock.Setup(c => c.Message).Returns(new CommitInventoryReservation(Guid.NewGuid()));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _dbMock.Setup(d => d.FindReservationByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((InventoryReservation?)null);

        await consumer.Consume(contextMock.Object);

        _publishEndpointMock.Verify(p => p.Publish(It.Is<InventoryReservationRejected>(r => r.Reason == "RESERVATION_NOT_FOUND"), It.IsAny<IPipe<PublishContext<InventoryReservationRejected>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitInventoryReservationConsumer_Success_PublishesCommitted()
    {
        var consumer = new CommitInventoryReservationConsumer(_dbMock.Object, _publishEndpointMock.Object, _leasePolicyMock.Object);
        var contextMock = new Mock<ConsumeContext<CommitInventoryReservation>>();
        var orderId = Guid.NewGuid();
        contextMock.Setup(c => c.Message).Returns(new CommitInventoryReservation(orderId));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        var reservation = InventoryReservation.Create(orderId, new Dictionary<string, int> { { "SKU1", 2 } }, now.AddMinutes(5));
        
        _dbMock.Setup(d => d.FindReservationByOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        _leasePolicyMock.Setup(l => l.UtcNow).Returns(now);

        await consumer.Consume(contextMock.Object);

        reservation.Status.ShouldBe(InventoryReservationStatus.Committed);
        _publishEndpointMock.Verify(p => p.Publish(It.Is<InventoryReservationCommitted>(c => c.OrderId == orderId && c.ReservationId == reservation.Id), It.IsAny<IPipe<PublishContext<InventoryReservationCommitted>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
