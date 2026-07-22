using Ordering.Application.Orders.Commands;
using Ordering.Application.Orders.Commands.CancelOrder;
using Ordering.Application.Orders.Commands.CreateOrder;
using Shouldly;
using Xunit;

namespace Ordering.UnitTests;

public class OrderingApplicationTests
{
    [Fact]
    public void CreateOrderCommandValidator_Should_Fail_When_IdempotencyKey_Is_Empty()
    {
        // Arrange
        var validator = new CreateOrderCommandValidator();
        var command = new CreateOrderCommand(Guid.Empty, Guid.Empty, string.Empty, null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "IdempotencyKey");
    }

    [Fact]
    public void CancelOrderCommandValidator_Should_Fail_When_OrderId_Is_Empty()
    {
        // Arrange
        var validator = new CancelOrderCommandValidator();
        var command = new CancelOrderCommand(Guid.Empty, "Reason");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "OrderId");
    }
}
