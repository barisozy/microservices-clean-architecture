using FluentValidation.Results;
using Fulfillment.Application.Common.Exceptions;
using Shouldly;
using Xunit;

namespace Fulfillment.UnitTests;

public class ApplicationExceptionTests
{
    [Fact]
    public void ValidationException_ShouldGroupFailures()
    {
        var exception = new ValidationException(
        [new ValidationFailure("OrderId", "Required"), new ValidationFailure("OrderId", "Invalid")]);

        exception.Errors["OrderId"].ShouldBe(["Required", "Invalid"]);
    }

    [Fact]
    public void NotFoundException_ShouldFormatEntityAndKey()
    {
        new NotFoundException().Message.ShouldNotBeNull();
        new NotFoundException("Shipment missing").Message.ShouldBe("Shipment missing");
        new NotFoundException("Shipment", 42).Message.ShouldBe("Entity \"Shipment\" (42) was not found.");
    }
}
