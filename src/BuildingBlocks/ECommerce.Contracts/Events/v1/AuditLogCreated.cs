namespace ECommerce.Contracts.Events.v1;

public record AuditLogCreated(
    Guid Id,
    string UserId,
    string UserRoles,
    string IpAddress,
    string UserAgent,
    string Action,
    string EntityName,
    string EntityId,
    string Changes,
    string TraceId,
    DateTimeOffset Timestamp
);
