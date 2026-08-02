using ECommerce.Contracts.Events.v1;
using MassTransit;
using Audit.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Audit.Api.Consumers;

public class PermissionDeniedConsumer(AuditDbContext dbContext) : IConsumer<PermissionDenied>
{
    public async Task Consume(ConsumeContext<PermissionDenied> context)
    {
        var msg = context.Message;
        var idempotencyKey = $"perm-denied-{msg.ActorSubject}-{msg.OccurredAt.ToUnixTimeMilliseconds()}";

        var exists = await dbContext.AuditLogs.AnyAsync(x => x.IdempotencyKey == idempotencyKey, context.CancellationToken);
        if (exists) return;

        var lastRecord = await dbContext.AuditLogs.OrderByDescending(x => x.Id).FirstOrDefaultAsync(context.CancellationToken);
        var prevHash = lastRecord?.RowHash ?? "GENESIS_HASH";

        var record = new AuditLogRecord
        {
            UserId = msg.ActorSubject,
            Action = "PermissionDenied",
            EntityName = "Permission",
            EntityId = msg.Permission,
            Changes = $"Resource: {msg.Resource}",
            Timestamp = msg.OccurredAt,
            IdempotencyKey = idempotencyKey,
            PrevHash = prevHash
        };

        record.RowHash = record.CalculateHash();

        dbContext.AuditLogs.Add(record);
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
