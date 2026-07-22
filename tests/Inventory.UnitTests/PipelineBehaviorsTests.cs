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
using Inventory.Application.Common.Behaviors;

namespace Inventory.UnitTests;

public class DummyRequest : IRequest<string> { }

public class PipelineBehaviorsTests
{
    [Fact]
    public async Task ValidationBehavior_Should_Throw_ValidationException_When_Validation_Fails()
    {
        var validatorMock = new Mock<IValidator<DummyRequest>>();
        validatorMock.Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<DummyRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("Prop", "Error") }));

        var behavior = new ValidationBehavior<DummyRequest, string>(new[] { validatorMock.Object });

        await Should.ThrowAsync<Inventory.Application.Common.Exceptions.ValidationException>(async () =>
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
