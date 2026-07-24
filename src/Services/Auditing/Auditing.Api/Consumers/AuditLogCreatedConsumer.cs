using ECommerce.Contracts.Events.v1;
using MassTransit;
using Auditing.Api.Data;

namespace Auditing.Api.Consumers;

public class AuditLogCreatedConsumer(AuditingDbContext dbContext) : IConsumer<AuditLogCreated>
{
    public async Task Consume(ConsumeContext<AuditLogCreated> context)
    {
        var msg = context.Message;
        dbContext.AuditLogs.Add(new AuditLogRecord
        {
            Id = msg.Id,
            UserId = msg.UserId,
            UserRoles = msg.UserRoles,
            IpAddress = msg.IpAddress,
            UserAgent = msg.UserAgent,
            Action = msg.Action,
            EntityName = msg.EntityName,
            EntityId = msg.EntityId,
            Changes = msg.Changes,
            TraceId = msg.TraceId,
            Timestamp = msg.Timestamp
        });

        await dbContext.SaveChangesAsync();
    }
}
