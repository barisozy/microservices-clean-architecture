using FluentValidation.Results;
using Payment.Application.Common.Exceptions;
using Shouldly;
using Xunit;

namespace Payment.UnitTests;

public class ApplicationExceptionTests
{
    [Fact]
    public void ValidationException_ShouldGroupFailuresByProperty()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Amount", "Must be positive"),
            new ValidationFailure("Amount", "Must be within the limit"),
            new ValidationFailure("Currency", "Required")
        ]);

        exception.Message.ShouldBe("One or more validation failures have occurred.");
        exception.Errors["Amount"].ShouldBe(["Must be positive", "Must be within the limit"]);
        exception.Errors["Currency"].ShouldBe(["Required"]);
    }

    [Fact]
    public void NotFoundException_Constructors_ShouldExposeUsefulMessage()
    {
        new NotFoundException().Message.ShouldNotBeNull();
        new NotFoundException("Payment missing").Message.ShouldBe("Payment missing");
        new NotFoundException("Payment", "payment-1").Message.ShouldBe("Entity \"Payment\" (payment-1) was not found.");
    }
}
