namespace Audit.Application.Common.Interfaces;

public sealed record AuditEntryWrite(
    string ActorSubject,
    string Action,
    string TargetType,
    string TargetId,
    string Outcome,
    DateTimeOffset OccurredAt,
    Guid IdempotencyKey);

public sealed record AuditEntryDto(
    long Id,
    string ActorSubject,
    string Action,
    string TargetType,
    string TargetId,
    string Outcome,
    DateTimeOffset OccurredAt,
    string RowHash,
    string PrevHash,
    Guid IdempotencyKey);

public sealed record AuditEntryPage(IReadOnlyList<AuditEntryDto> Items, long? NextCursor);
public sealed record AuditVerificationResult(bool Valid, long? BrokenAtId, string? ErrorMessage);

public interface IAuditEntryStore
{
    Task AppendAsync(AuditEntryWrite entry, CancellationToken cancellationToken);

    Task<AuditEntryPage> QueryAsync(
        string? actor,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        long? cursor,
        int limit,
        CancellationToken cancellationToken);

    Task<AuditVerificationResult> VerifyAsync(long? fromId, long? toId, CancellationToken cancellationToken);
}

public interface IIamPermissionChecker
{
    Task<bool> IsAllowedAsync(string subject, string permission, CancellationToken cancellationToken);
}
