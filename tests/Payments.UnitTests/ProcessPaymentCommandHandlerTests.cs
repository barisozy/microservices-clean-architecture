using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.EntityFrameworkCore;
using Payments.Application.Common.Interfaces;
using Payments.Application.Payments.Commands;
using Payments.Domain.Entities;
using Shouldly;
using Xunit;

namespace Payments.UnitTests;

public class ProcessPaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_Complete_Payment_When_No_Fail_Flag()
    {
        var contextMock = new Mock<IPaymentsDbContext>();
        var publishMock = new Mock<IPublishEndpoint>();

        contextMock.Setup(x => x.Payments).ReturnsDbSet(new List<PaymentRecord>());
        
        var paymentList = new List<PaymentRecord>();
        contextMock.Setup(x => x.Payments.Add(It.IsAny<PaymentRecord>())).Callback<PaymentRecord>(p => paymentList.Add(p));

        var readRepoMock = new Mock<IPaymentReadRepository>();

        var handler = new ProcessPaymentCommandHandler(contextMock.Object, publishMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ProcessPaymentCommand(Guid.NewGuid(), "key-123", 100, new List<OrderItemContractDto>
        {
            new OrderItemContractDto("SKU-1", 1, 100)
        }, DateTimeOffset.UtcNow), CancellationToken.None);

        result.ShouldNotBe(Guid.Empty);
        contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publishMock.Verify(x => x.Publish(It.IsAny<PaymentCompleted>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Fail_Payment_When_Fail_Flag_Present()
    {
        var contextMock = new Mock<IPaymentsDbContext>();
        var publishMock = new Mock<IPublishEndpoint>();

        contextMock.Setup(x => x.Payments).ReturnsDbSet(new List<PaymentRecord>());

        var readRepoMock = new Mock<IPaymentReadRepository>();

        var handler = new ProcessPaymentCommandHandler(contextMock.Object, publishMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ProcessPaymentCommand(Guid.NewGuid(), "key-456", 100, new List<OrderItemContractDto>
        {
            new OrderItemContractDto("FAIL_PAYMENT_SKU", 1, 100)
        }, DateTimeOffset.UtcNow), CancellationToken.None);

        result.ShouldNotBe(Guid.Empty);
        contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publishMock.Verify(x => x.Publish(It.IsAny<PaymentFailed>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
