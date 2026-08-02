using Audit.Application.AuditEntries;
using Audit.Application.Common.Interfaces;
using Audit.Domain.Entities;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Moq;
using Shouldly;
using Xunit;

namespace Audit.UnitTests;

public sealed class AuditEntryTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 8, 2, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldBuildDeterministicHashChainEntry()
    {
        var key = Guid.NewGuid();

        var entry = AuditEntry.Create(
            "operator-1", "IAM.PermissionDenied", "Permission", "Catalog.Write", "Denied",
            OccurredAt, AuditEntry.GenesisHash, key);

        entry.ActorSubject.ShouldBe("operator-1");
        entry.PrevHash.ShouldBe(AuditEntry.GenesisHash);
        entry.IdempotencyKey.ShouldBe(key);
        entry.OccurredAt.ShouldBe(OccurredAt);
        entry.RowHash.ShouldBe(entry.RecalculateHash());
        entry.RowHash.Length.ShouldBe(64);
    }

    [Theory]
    [InlineData("", "action", "type", "id", "outcome")]
    [InlineData("actor", "", "type", "id", "outcome")]
    [InlineData("actor", "action", "", "id", "outcome")]
    [InlineData("actor", "action", "type", "", "outcome")]
    [InlineData("actor", "action", "type", "id", "")]
    public void Create_ShouldRejectMissingRequiredFields(
        string actor, string action, string targetType, string targetId, string outcome)
    {
        Should.Throw<ArgumentException>(() => AuditEntry.Create(
            actor, action, targetType, targetId, outcome, OccurredAt, AuditEntry.GenesisHash, Guid.NewGuid()));
    }

    [Fact]
    public void Create_ShouldRejectInvalidPreviousHash()
    {
        Should.Throw<ArgumentException>(() => AuditEntry.Create(
            "actor", "action", "type", "id", "outcome", OccurredAt, "short", Guid.NewGuid()));
    }

    [Fact]
    public void CalculateHash_ShouldNormalizeTimestampToUtc()
    {
        var localOffset = OccurredAt.ToOffset(TimeSpan.FromHours(3));

        AuditEntry.CalculateHash(AuditEntry.GenesisHash, "actor", "action", "type", "id", "outcome", localOffset)
            .ShouldBe(AuditEntry.CalculateHash(
                AuditEntry.GenesisHash, "actor", "action", "type", "id", "outcome", OccurredAt));
    }
}

public sealed class AuditApplicationTests
{
    [Fact]
    public async Task GetAuditEntriesQueryHandler_ShouldClampLimitAndForwardFilters()
    {
        var store = new Mock<IAuditEntryStore>();
        var expected = new AuditEntryPage([], 42);
        store.Setup(x => x.QueryAsync("actor", "action", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), 7, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow;

        var result = await new GetAuditEntriesQueryHandler(store.Object).Handle(
            new GetAuditEntriesQuery("actor", "action", from, to, 7, 500), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        store.VerifyAll();
    }

    [Fact]
    public async Task GetAuditEntriesQueryHandler_ShouldUseMinimumLimit()
    {
        var store = new Mock<IAuditEntryStore>();
        store.Setup(x => x.QueryAsync(null, null, null, null, null, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditEntryPage([], null));

        await new GetAuditEntriesQueryHandler(store.Object).Handle(
            new GetAuditEntriesQuery(null, null, null, null, null, 0), TestContext.Current.CancellationToken);

        store.VerifyAll();
    }

    [Fact]
    public async Task PermissionDeniedConsumer_ShouldAppendMappedEntryUsingMessageId()
    {
        var store = RecordingStore(out var capture);
        var messageId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var message = new PermissionDenied("operator-1", "Catalog.Write", "SKU-1", occurredAt);

        await new Audit.Application.Consumers.PermissionDeniedConsumer(store.Object)
            .Consume(ConsumerContext(message, messageId).Object);

        capture.Value!.Action.ShouldBe("IAM.PermissionDenied");
        capture.Value.TargetType.ShouldBe("Permission");
        capture.Value.TargetId.ShouldBe("Catalog.Write");
        capture.Value.Outcome.ShouldBe("Denied");
        capture.Value.IdempotencyKey.ShouldBe(messageId);
    }

    [Fact]
    public async Task CouponWrittenConsumer_ShouldAppendMappedEntry()
    {
        var store = RecordingStore(out var capture);
        var message = new CouponWritten("admin-1", "SUMMER20", "Created", DateTimeOffset.UtcNow);

        await new Audit.Application.Consumers.CouponWrittenConsumer(store.Object)
            .Consume(ConsumerContext(message, null).Object);

        capture.Value!.Action.ShouldBe("Promotion.CouponWritten");
        capture.Value.TargetType.ShouldBe("Coupon");
        capture.Value.TargetId.ShouldBe("SUMMER20");
        capture.Value.Outcome.ShouldBe("Success");
        capture.Value.IdempotencyKey.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task UserRegisteredConsumer_ShouldAppendUserAuditEntry()
    {
        var store = RecordingStore(out var capture);
        var subject = Guid.NewGuid();

        await new Audit.Application.Consumers.UserRegisteredConsumer(store.Object)
            .Consume(ConsumerContext(new UserRegistered(subject, "user@example.test"), null).Object);

        capture.Value!.ActorSubject.ShouldBe(subject.ToString("D"));
        capture.Value.Action.ShouldBe("IAM.UserRegistered");
        capture.Value.TargetType.ShouldBe("User");
        capture.Value.TargetId.ShouldBe(subject.ToString("D"));
        capture.Value.Outcome.ShouldBe("Success");
    }

    private static Mock<IAuditEntryStore> RecordingStore(out EntryCapture capture)
    {
        var result = new EntryCapture();
        capture = result;
        var store = new Mock<IAuditEntryStore>();
        store.Setup(x => x.AppendAsync(It.IsAny<AuditEntryWrite>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntryWrite, CancellationToken>((value, _) => result.Value = value)
            .Returns(Task.CompletedTask);
        return store;
    }

    private sealed class EntryCapture
    {
        public AuditEntryWrite? Value { get; set; }
    }

    private static Mock<ConsumeContext<T>> ConsumerContext<T>(T message, Guid? messageId) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.MessageId).Returns(messageId);
        context.SetupGet(x => x.CancellationToken).Returns(TestContext.Current.CancellationToken);
        return context;
    }
}
