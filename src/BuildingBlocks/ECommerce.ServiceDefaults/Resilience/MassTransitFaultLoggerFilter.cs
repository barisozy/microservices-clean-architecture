using MassTransit;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ECommerce.ServiceDefaults.Resilience;

/// <summary>
/// Proactively logs exceptions before messages are moved to the Dead Letter Queue (DLQ).
/// Ensures Log Correlation with OpenTelemetry Trace ID.
/// </summary>
public class MassTransitFaultLoggerFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly ILogger<MassTransitFaultLoggerFilter<T>> _logger;

    public MassTransitFaultLoggerFilter(ILogger<MassTransitFaultLoggerFilter<T>> logger)
    {
        _logger = logger;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.TraceId.ToString() ?? "NoTraceId";
            
            // Explicit log correlation for DLQ alerting
            _logger.LogError(
                ex, 
                "DLQ ALERT: Message {MessageId} of type {MessageType} failed to consume. TraceId: {TraceId}", 
                context.MessageId, 
                typeof(T).Name,
                traceId);

            throw; // Let MassTransit handle the _error queue transition
        }
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("faultLogger");
    }
}
