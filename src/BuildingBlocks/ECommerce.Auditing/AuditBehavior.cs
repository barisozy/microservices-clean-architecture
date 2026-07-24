using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace ECommerce.Auditing;

public class AuditBehavior<TRequest, TResponse>(IPublishEndpoint publishEndpoint, IHttpContextAccessor httpContextAccessor) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        // Don't audit queries. Only Commands.
        if (!requestName.EndsWith("Command"))
            return await next();

        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
        var userRoles = string.Join(",", httpContextAccessor.HttpContext?.User?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value) ?? Array.Empty<string>());
        var ipAddress = httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = httpContextAccessor.HttpContext?.Request?.Headers?.UserAgent.ToString() ?? "Unknown";
        var traceId = Activity.Current?.Id ?? httpContextAccessor.HttpContext?.TraceIdentifier ?? "Unknown";

        var auditLog = new AuditLogCreated(
            Guid.NewGuid(),
            userId,
            userRoles,
            ipAddress,
            userAgent,
            "Execute",
            requestName,
            "N/A",
            JsonSerializer.Serialize(request),
            traceId,
            DateTimeOffset.UtcNow
        );

        await publishEndpoint.Publish(auditLog, cancellationToken);

        return await next();
    }
}
