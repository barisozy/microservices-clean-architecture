using FluentValidation.Results;
using Inventory.Application.Common.Exceptions;
using Inventory.Domain.Exceptions;
using Shouldly;
using Xunit;

namespace Inventory.UnitTests;

public class ApplicationExceptionTests
{
    [Fact]
    public void ValidationException_ShouldGroupValidationFailures()
    {
        var exception = new ValidationException(
        [new ValidationFailure("Sku", "Required"), new ValidationFailure("Sku", "Invalid")]);

        exception.Errors["Sku"].ShouldBe(["Required", "Invalid"]);
    }

    [Fact]
    public void NotFoundAndDomainExceptions_ShouldPreserveMessagesAndInnerExceptions()
    {
        new NotFoundException("Stock", "SKU-1").Message.ShouldBe("Entity \"Stock\" (SKU-1) was not found.");
        new NotFoundException("Missing").Message.ShouldBe("Missing");

        var cause = new InvalidOperationException("database unavailable");
        var exception = new InventoryDomainException("Could not reserve stock", cause);
        exception.Message.ShouldBe("Could not reserve stock");
        exception.InnerException.ShouldBe(cause);
    }
}
