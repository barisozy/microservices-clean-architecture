using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using Audit.Application.Common.Interfaces;
using ECommerce.Contracts.Events.v1;
using MassTransit;

namespace Audit.Application.Consumers;

public static class AuditMetrics
{
    private static readonly Meter Meter = new("Audit.Api");
    public static readonly Histogram<double> IngestDuration =
        Meter.CreateHistogram<double>("audit.entry_ingest.duration", "ms");
    public static readonly Counter<long> BrokenHashChain =
        Meter.CreateCounter<long>("audit.hash_chain.broken");
}

public sealed class PermissionDeniedConsumer(IAuditEntryStore store) : IConsumer<PermissionDenied>
{
    public Task Consume(ConsumeContext<PermissionDenied> context) => AppendMeasuredAsync(
        store,
        new AuditEntryWrite(
            context.Message.ActorSubject,
            "IAM.PermissionDenied",
            "Permission",
            context.Message.Permission,
            "Denied",
            context.Message.OccurredAt,
            MessageId(context, context.Message)),
        context.CancellationToken);

    private static Guid MessageId<T>(ConsumeContext<T> context, T message) where T : class =>
        context.MessageId ?? DeterministicId(message);

    internal static Guid DeterministicId<T>(T message)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(message?.ToString() ?? typeof(T).FullName!));
        return new Guid(hash.AsSpan(0, 16));
    }

    internal static async Task AppendMeasuredAsync(
        IAuditEntryStore store,
        AuditEntryWrite entry,
        CancellationToken cancellationToken)
    {
        var startedAt = TimeProvider.System.GetTimestamp();
        await store.AppendAsync(entry, cancellationToken);
        AuditMetrics.IngestDuration.Record(TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);
    }
}

public sealed class CouponWrittenConsumer(IAuditEntryStore store) : IConsumer<CouponWritten>
{
    public Task Consume(ConsumeContext<CouponWritten> context) => PermissionDeniedConsumer.AppendMeasuredAsync(
        store,
        new AuditEntryWrite(
            context.Message.ActorSubject,
            "Promotion.CouponWritten",
            "Coupon",
            context.Message.Code,
            "Success",
            context.Message.OccurredAt,
            context.MessageId ?? PermissionDeniedConsumer.DeterministicId(context.Message)),
        context.CancellationToken);
}

public sealed class UserRegisteredConsumer(IAuditEntryStore store) : IConsumer<UserRegistered>
{
    public Task Consume(ConsumeContext<UserRegistered> context) => PermissionDeniedConsumer.AppendMeasuredAsync(
        store,
        new AuditEntryWrite(
            context.Message.KeycloakSubject.ToString("D"),
            "IAM.UserRegistered",
            "User",
            context.Message.KeycloakSubject.ToString("D"),
            "Success",
            DateTimeOffset.UtcNow,
            context.MessageId ?? PermissionDeniedConsumer.DeterministicId(context.Message)),
        context.CancellationToken);
}
