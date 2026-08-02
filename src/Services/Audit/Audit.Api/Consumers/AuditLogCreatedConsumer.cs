using ECommerce.Contracts.Events.v1;
using MassTransit;
using Audit.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Audit.Api.Consumers;

public class AuditLogCreatedConsumer(AuditDbContext dbContext) : IConsumer<AuditLogCreated>
{
    public async Task Consume(ConsumeContext<AuditLogCreated> context)
    {
        var msg = context.Message;
        var idempotencyKey = msg.Id.ToString();

        var exists = await dbContext.AuditLogs.AnyAsync(x => x.IdempotencyKey == idempotencyKey, context.CancellationToken);
        if (exists) return;

        var lastRecord = await dbContext.AuditLogs.OrderByDescending(x => x.Id).FirstOrDefaultAsync(context.CancellationToken);
        var prevHash = lastRecord?.RowHash ?? "GENESIS_HASH";

        var record = new AuditLogRecord
        {
            UserId = msg.UserId,
            UserRoles = msg.UserRoles,
            IpAddress = msg.IpAddress,
            UserAgent = msg.UserAgent,
            Action = msg.Action,
            EntityName = msg.EntityName,
            EntityId = msg.EntityId,
            Changes = msg.Changes,
            TraceId = msg.TraceId,
            Timestamp = msg.Timestamp,
            IdempotencyKey = idempotencyKey,
            PrevHash = prevHash
        };

        record.RowHash = record.CalculateHash();

        dbContext.AuditLogs.Add(record);
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}

