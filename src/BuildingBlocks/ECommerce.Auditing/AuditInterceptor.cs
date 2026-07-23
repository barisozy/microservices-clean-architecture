using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace ECommerce.Auditing;

public class AuditInterceptor(IPublishEndpoint publishEndpoint, IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
        var userRoles = string.Join(",", httpContextAccessor.HttpContext?.User?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value) ?? Array.Empty<string>());
        var ipAddress = httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = httpContextAccessor.HttpContext?.Request?.Headers?.UserAgent.ToString() ?? "Unknown";
        var traceId = Activity.Current?.Id ?? httpContextAccessor.HttpContext?.TraceIdentifier ?? "Unknown";

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType().Name;
            if (entityType.Contains("AuditLog") || entityType.Contains("Outbox") || entityType.Contains("Inbox"))
                continue;

            var action = entry.State.ToString();
            var entityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "Unknown";
            
            var changes = new Dictionary<string, object?>();
            foreach (var property in entry.Properties)
            {
                if (property.IsModified || entry.State == EntityState.Added || entry.State == EntityState.Deleted)
                {
                    changes[property.Metadata.Name] = new { 
                        OldValue = entry.State == EntityState.Added ? null : property.OriginalValue, 
                        NewValue = entry.State == EntityState.Deleted ? null : property.CurrentValue 
                    };
                }
            }

            var auditLog = new AuditLogCreated(
                Guid.NewGuid(),
                userId,
                userRoles,
                ipAddress,
                userAgent,
                action,
                entityType,
                entityId,
                JsonSerializer.Serialize(changes),
                traceId,
                DateTimeOffset.UtcNow
            );

            await publishEndpoint.Publish(auditLog, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
