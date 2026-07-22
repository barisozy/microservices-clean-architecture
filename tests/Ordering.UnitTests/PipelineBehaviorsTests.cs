using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;
using Ordering.Application.Common.Behaviors;

namespace Ordering.UnitTests;

public class DummyRequest : IRequest<string> { }

public class ExceptionTests
{
    [Fact]
    public void ValidationException_Empty_Constructor_Creates_Empty_Errors()
    {
        var ex = new Ordering.Application.Common.Exceptions.ValidationException();
        ex.Errors.ShouldNotBeNull();
        ex.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void NotFoundException_Constructors_Should_Set_Properties()
    {
        var ex1 = new Ordering.Application.Common.Exceptions.NotFoundException();
        ex1.Message.ShouldNotBeNull();

        var ex2 = new Ordering.Application.Common.Exceptions.NotFoundException("message");
        ex2.Message.ShouldBe("message");

        var inner = new Exception("inner");
        var ex3 = new Ordering.Application.Common.Exceptions.NotFoundException("message", inner);
        ex3.InnerException.ShouldBe(inner);

        var ex4 = new Ordering.Application.Common.Exceptions.NotFoundException("Entity", 123);
        ex4.Message.ShouldBe("Entity \"Entity\" (123) was not found.");
    }
}

public class PipelineBehaviorsTests
{
    [Fact]
    public async Task ValidationBehavior_Should_Throw_ValidationException_When_Validation_Fails()
    {
        var validatorMock = new Mock<IValidator<DummyRequest>>();
        validatorMock.Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<DummyRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("Prop", "Error") }));

        var behavior = new ValidationBehavior<DummyRequest, string>(new[] { validatorMock.Object });

        await Should.ThrowAsync<Ordering.Application.Common.Exceptions.ValidationException>(async () =>
            await behavior.Handle(new DummyRequest(), null!, CancellationToken.None));
    }

    [Fact]
    public async Task LoggingBehavior_Should_Process_Without_Errors()
    {
        var loggerMock = new Mock<ILogger<DummyRequest>>();
        var behavior = new LoggingBehavior<DummyRequest>(loggerMock.Object);
        await behavior.Process(new DummyRequest(), CancellationToken.None);
    }

    [Fact]
    public async Task UnhandledExceptionBehavior_Should_Rethrow_Exception()
    {
        var loggerMock = new Mock<ILogger<DummyRequest>>();
        var behavior = new UnhandledExceptionBehavior<DummyRequest, string>(loggerMock.Object);

        var nextMock = new Mock<RequestHandlerDelegate<string>>();
        nextMock.Setup(x => x()).ThrowsAsync(new Exception("Test error"));

        await Should.ThrowAsync<Exception>(async () =>
            await behavior.Handle(new DummyRequest(), nextMock.Object, CancellationToken.None));
    }
}
