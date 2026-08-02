using Audit.Api.Consumers;
using Audit.Api.Data;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Xunit;

namespace Audit.UnitTests;

public class SecurityAuditConsumersTests
{
    [Fact]
    public void CalculateHash_ShouldBeDeterministic_AndDetectTamperedPayload()
    {
        var record = new AuditLogRecord
        {
            UserId = "auditor-1", Action = "Updated", EntityName = "Coupon", EntityId = "SUMMER20",
            Changes = "{\"value\":20}", Timestamp = DateTimeOffset.UnixEpoch,
            IdempotencyKey = "audit-entry-1", PrevHash = "GENESIS_HASH"
        };

        var originalHash = record.CalculateHash();

        record.CalculateHash().ShouldBe(originalHash);
        record.Changes = "{\"value\":25}";
        record.CalculateHash().ShouldNotBe(originalHash);
    }

    [Fact]
    public async Task PermissionDeniedConsumer_ShouldAppendAHashChainedRecordAndIgnoreDuplicate()
    {
        await using var db = CreateContext();
        db.AuditLogs.Add(CreatePriorRecord());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var occurredAt = DateTimeOffset.UtcNow;
        var message = new PermissionDenied("user-1", "Catalog.Write", "product-42", occurredAt);
        var context = ConsumerContext(message);
        var consumer = new PermissionDeniedConsumer(db);

        await consumer.Consume(context.Object);
        await consumer.Consume(context.Object);

        var records = await db.AuditLogs.OrderBy(entry => entry.Id).ToListAsync(TestContext.Current.CancellationToken);
        records.Count.ShouldBe(2);
        var appended = records.Last();
        appended.PrevHash.ShouldBe("previous-hash");
        appended.RowHash.ShouldBe(appended.CalculateHash());
        appended.Action.ShouldBe("PermissionDenied");
    }

    [Fact]
    public async Task PermissionDeniedConsumer_ShouldUseGenesisHash_WhenNoPreviousRecordExists()
    {
        await using var db = CreateContext();
        var message = new PermissionDenied("user-1", "Catalog.Read", "product-1", DateTimeOffset.UtcNow);

        await new PermissionDeniedConsumer(db).Consume(ConsumerContext(message).Object);

        var record = await db.AuditLogs.SingleAsync(TestContext.Current.CancellationToken);
        record.PrevHash.ShouldBe("GENESIS_HASH");
        record.RowHash.ShouldBe(record.CalculateHash());
    }

    [Fact]
    public async Task CouponWrittenConsumer_ShouldCreateGenesisRecordAndIgnoreDuplicate()
    {
        await using var db = CreateContext();
        var occurredAt = DateTimeOffset.UtcNow;
        var message = new CouponWritten("admin-1", "SUMMER20", "Created", occurredAt);
        var context = ConsumerContext(message);
        var consumer = new CouponWrittenConsumer(db);

        await consumer.Consume(context.Object);
        await consumer.Consume(context.Object);

        var record = await db.AuditLogs.SingleAsync(TestContext.Current.CancellationToken);
        record.PrevHash.ShouldBe("GENESIS_HASH");
        record.RowHash.ShouldBe(record.CalculateHash());
        record.EntityName.ShouldBe("Coupon");
        record.EntityId.ShouldBe("SUMMER20");
    }

    [Fact]
    public async Task CouponWrittenConsumer_ShouldChainToTheLatestExistingRecord()
    {
        await using var db = CreateContext();
        db.AuditLogs.Add(CreatePriorRecord());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var message = new CouponWritten("admin-1", "SUMMER20", "Updated", DateTimeOffset.UtcNow);

        await new CouponWrittenConsumer(db).Consume(ConsumerContext(message).Object);

        var appended = await db.AuditLogs.OrderByDescending(record => record.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        appended.PrevHash.ShouldBe("previous-hash");
        appended.RowHash.ShouldBe(appended.CalculateHash());
    }

    [Fact]
    public async Task AuditLogCreatedConsumer_ShouldIgnoreDuplicate_AndKeepTheOriginalHashChainEntry()
    {
        await using var db = CreateContext();
        var message = new AuditLogCreated(
            Guid.NewGuid(), "user-1", "Admin", "127.0.0.1", "test-agent", "Created",
            "Order", "order-1", "{}", "trace-1", DateTimeOffset.UtcNow);
        var context = ConsumerContext(message);
        var consumer = new AuditLogCreatedConsumer(db);

        await consumer.Consume(context.Object);
        await consumer.Consume(context.Object);

        var records = await db.AuditLogs.ToListAsync(TestContext.Current.CancellationToken);
        records.Count.ShouldBe(1);
        records[0].PrevHash.ShouldBe("GENESIS_HASH");
        records[0].RowHash.ShouldBe(records[0].CalculateHash());
    }

    [Fact]
    public async Task AuditLogCreatedConsumer_ShouldChainToTheMostRecentRecord()
    {
        await using var db = CreateContext();
        db.AuditLogs.Add(CreatePriorRecord());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var message = new AuditLogCreated(
            Guid.NewGuid(), "user-1", string.Empty, string.Empty, string.Empty, "Updated", "Order", "order-1", "{}", string.Empty,
            DateTimeOffset.UtcNow);

        await new AuditLogCreatedConsumer(db).Consume(ConsumerContext(message).Object);

        var appended = await db.AuditLogs.OrderByDescending(record => record.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        appended.PrevHash.ShouldBe("previous-hash");
        appended.RowHash.ShouldBe(appended.CalculateHash());
    }

    private static AuditDbContext CreateContext() => new(new DbContextOptionsBuilder<AuditDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static AuditLogRecord CreatePriorRecord() => new()
    {
        UserId = "previous-user", Action = "Created", EntityName = "Order", EntityId = "order-1",
        Changes = "{}", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1), IdempotencyKey = "previous",
        RowHash = "previous-hash", PrevHash = "GENESIS_HASH"
    };

    private static Mock<ConsumeContext<T>> ConsumerContext<T>(T message) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(value => value.Message).Returns(message);
        context.SetupGet(value => value.CancellationToken).Returns(TestContext.Current.CancellationToken);
        return context;
    }
}
