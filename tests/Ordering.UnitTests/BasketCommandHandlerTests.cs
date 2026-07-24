using System.Collections.Generic;
using System.Threading.Tasks;
using Ordering.Application.Basket.Commands;
using Ordering.Application.Common.Interfaces;
using Moq;
using Shouldly;
using Xunit;

namespace Ordering.UnitTests;

public class BasketCommandHandlerTests
{
    [Fact]
    public async Task UpdateBasketCommandHandler_ShouldReturnTrue_WhenBasketServiceNotNull()
    {
        // Arrange
        var basketServiceMock = new Mock<IBasketService>();
        var handler = new UpdateBasketCommandHandler(basketServiceMock.Object);
        var command = new UpdateBasketCommand("buyer-1", new List<UpdateBasketItemDto> { new("SKU-1", 2) });

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteBasketCommandHandler_ShouldDelegateToBasketService()
    {
        // Arrange
        var basketServiceMock = new Mock<IBasketService>();
        basketServiceMock.Setup(x => x.DeleteBasketAsync("buyer-1", It.IsAny<CancellationToken>()))
                         .ReturnsAsync(true);

        var handler = new DeleteBasketCommandHandler(basketServiceMock.Object);
        var command = new DeleteBasketCommand("buyer-1");

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        basketServiceMock.Verify(x => x.DeleteBasketAsync("buyer-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
