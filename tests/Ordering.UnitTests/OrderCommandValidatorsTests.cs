using System;
using Ordering.Application.Orders.Commands;
using Ordering.Application.Orders.Commands.CancelOrder;
using Ordering.Application.Orders.Commands.CreateOrder;
using Shouldly;
using Xunit;

namespace Ordering.UnitTests;

public class OrderCommandValidatorsTests
{
    [Fact]
    public void CreateOrderCommandValidator_Should_Have_Error_When_IdempotencyKey_Is_Empty()
    {
        var validator = new CreateOrderCommandValidator();
        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "", new System.Collections.Generic.List<OrderItemDto>());
        
        var result = validator.Validate(command);
        
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "IdempotencyKey");
    }

    [Fact]
    public void CancelOrderCommandValidator_Should_Have_Error_When_OrderId_Is_Empty()
    {
        var validator = new CancelOrderCommandValidator();
        var command = new CancelOrderCommand(Guid.Empty, "reason");
        
        var result = validator.Validate(command);
        
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "OrderId");
    }

    [Fact]
    public void CancelOrderCommandValidator_Should_Have_Error_When_Reason_Is_Empty()
    {
        var validator = new CancelOrderCommandValidator();
        var command = new CancelOrderCommand(Guid.NewGuid(), "");
        
        var result = validator.Validate(command);
        
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Reason");
    }
}
