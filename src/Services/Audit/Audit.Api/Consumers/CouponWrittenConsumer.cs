using ECommerce.Contracts.Events.v1;
using MassTransit;
using Audit.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Audit.Api.Consumers;

public class CouponWrittenConsumer(AuditDbContext dbContext) : IConsumer<CouponWritten>
{
    public async Task Consume(ConsumeContext<CouponWritten> context)
    {
        var msg = context.Message;
        var idempotencyKey = $"coupon-written-{msg.Code}-{msg.OccurredAt.ToUnixTimeMilliseconds()}";

        var exists = await dbContext.AuditLogs.AnyAsync(x => x.IdempotencyKey == idempotencyKey, context.CancellationToken);
        if (exists) return;

        var lastRecord = await dbContext.AuditLogs.OrderByDescending(x => x.Id).FirstOrDefaultAsync(context.CancellationToken);
        var prevHash = lastRecord?.RowHash ?? "GENESIS_HASH";

        var record = new AuditLogRecord
        {
            UserId = msg.ActorSubject,
            Action = msg.Action,
            EntityName = "Coupon",
            EntityId = msg.Code,
            Changes = $"Coupon: {msg.Code}, Action: {msg.Action}",
            Timestamp = msg.OccurredAt,
            IdempotencyKey = idempotencyKey,
            PrevHash = prevHash
        };

        record.RowHash = record.CalculateHash();

        dbContext.AuditLogs.Add(record);
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}

