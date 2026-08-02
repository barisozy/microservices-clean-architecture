using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Payment.Application.Common.Interfaces;
using Payment.Application.Payment.Queries;
using Shouldly;
using Xunit;

namespace Payment.UnitTests;

public class GetPaymenttatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnStatus_WhenOrderExists()
    {
        var readRepositoryMock = new Mock<IPaymentReadRepository>();
        var orderId = Guid.NewGuid();

        readRepositoryMock.Setup(x => x.GetPaymenttatusAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Success");

        var handler = new GetPaymenttatusQueryHandler(readRepositoryMock.Object);
        var result = await handler.Handle(new GetPaymenttatusQuery(orderId), CancellationToken.None);

        result.ShouldBe("Success");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenOrderDoesNotExist()
    {
        var readRepositoryMock = new Mock<IPaymentReadRepository>();
        var orderId = Guid.NewGuid();

        readRepositoryMock.Setup(x => x.GetPaymenttatusAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var handler = new GetPaymenttatusQueryHandler(readRepositoryMock.Object);
        var result = await handler.Handle(new GetPaymenttatusQuery(orderId), CancellationToken.None);

        result.ShouldBeNull();
    }
}

