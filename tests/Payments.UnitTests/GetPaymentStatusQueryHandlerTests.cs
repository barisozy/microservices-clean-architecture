using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Payments.Application.Common.Interfaces;
using Payments.Application.Payments.Queries;
using Shouldly;
using Xunit;

namespace Payments.UnitTests;

public class GetPaymentStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnStatus_WhenOrderExists()
    {
        var readRepositoryMock = new Mock<IPaymentReadRepository>();
        var orderId = Guid.NewGuid();

        readRepositoryMock.Setup(x => x.GetPaymentStatusAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Success");

        var handler = new GetPaymentStatusQueryHandler(readRepositoryMock.Object);
        var result = await handler.Handle(new GetPaymentStatusQuery(orderId), CancellationToken.None);

        result.ShouldBe("Success");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenOrderDoesNotExist()
    {
        var readRepositoryMock = new Mock<IPaymentReadRepository>();
        var orderId = Guid.NewGuid();

        readRepositoryMock.Setup(x => x.GetPaymentStatusAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var handler = new GetPaymentStatusQueryHandler(readRepositoryMock.Object);
        var result = await handler.Handle(new GetPaymentStatusQuery(orderId), CancellationToken.None);

        result.ShouldBeNull();
    }
}
