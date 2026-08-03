using MediatR;
using MediatR.Pipeline;
using Microsoft.Extensions.Logging;

namespace Order.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest>(ILogger<TRequest> logger) : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling Order request {RequestName}", typeof(TRequest).Name);
        return Task.CompletedTask;
    }
}
