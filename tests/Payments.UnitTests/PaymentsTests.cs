using Payments.Domain.Entities;
using Payments.Application.Payments.Commands;
using ECommerce.Contracts.Events;
using Shouldly;
using Xunit;
using System.Collections.Generic;
using System;

namespace Payments.UnitTests;

public class PaymentsTests
{
    [Fact]
    public void PaymentRecord_Create_Should_Initialize_With_Pending_Status()
    {
        var orderId = Guid.NewGuid();
        var payment = PaymentRecord.Create(orderId, "key123", 100.50m);
        payment.Status.ShouldBe("Pending");
        payment.Amount.ShouldBe(100.50m);
    }

    [Fact]
    public void PaymentRecord_Complete_Should_Set_Status_To_Completed()
    {
        var payment = PaymentRecord.Create(Guid.NewGuid(), "key", 10);
        payment.Complete();
        payment.Status.ShouldBe("Completed");
    }

    [Fact]
    public void ProcessPaymentCommand_Should_Store_Values_Correctly()
    {
        var command = new ProcessPaymentCommand(Guid.NewGuid(), "key123", 50m, new List<OrderItemContractDto>());
        command.IdempotencyKey.ShouldBe("key123");
        command.Amount.ShouldBe(50m);
    }
}
