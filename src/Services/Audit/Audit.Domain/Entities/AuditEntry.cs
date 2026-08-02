using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Audit.Domain.Entities;

public sealed class AuditEntry
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private AuditEntry() { }

    public long Id { get; private set; }
    public string ActorSubject { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string RowHash { get; private set; } = string.Empty;
    public string PrevHash { get; private set; } = string.Empty;
    public Guid IdempotencyKey { get; private set; }

    public static AuditEntry Create(
        string actorSubject,
        string action,
        string targetType,
        string targetId,
        string outcome,
        DateTimeOffset occurredAt,
        string previousHash,
        Guid idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        if (previousHash.Length != 64)
        {
            throw new ArgumentException("Previous hash must contain 64 hexadecimal characters.", nameof(previousHash));
        }

        var normalizedOccurredAt = occurredAt.ToUniversalTime();
        return new AuditEntry
        {
            ActorSubject = actorSubject,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Outcome = outcome,
            OccurredAt = normalizedOccurredAt,
            PrevHash = previousHash,
            IdempotencyKey = idempotencyKey,
            RowHash = CalculateHash(
                previousHash,
                actorSubject,
                action,
                targetType,
                targetId,
                outcome,
                normalizedOccurredAt)
        };
    }

    public string RecalculateHash() => CalculateHash(
        PrevHash,
        ActorSubject,
        Action,
        TargetType,
        TargetId,
        Outcome,
        OccurredAt);

    public static string CalculateHash(
        string previousHash,
        string actorSubject,
        string action,
        string targetType,
        string targetId,
        string outcome,
        DateTimeOffset occurredAt)
    {
        var payload = string.Concat(
            previousHash,
            actorSubject,
            action,
            targetType,
            targetId,
            outcome,
            occurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
