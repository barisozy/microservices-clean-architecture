using System.Data;
using Audit.Application.Common.Interfaces;
using Audit.Application.Consumers;
using Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Audit.Infrastructure.Data;

public sealed class AuditEntryStore(AuditDbContext dbContext) : IAuditEntryStore
{
    public async Task AppendAsync(AuditEntryWrite entry, CancellationToken cancellationToken)
    {
        IDbContextTransaction? ownedTransaction = null;
        if (dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null)
        {
            ownedTransaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        }

        try
        {
            if (dbContext.Database.IsNpgsql())
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(hashtext('audit.AuditEntries'));",
                    cancellationToken);
            }

            if (await dbContext.AuditEntries
                .AsNoTracking()
                .AnyAsync(candidate => candidate.IdempotencyKey == entry.IdempotencyKey, cancellationToken))
            {
                if (ownedTransaction is not null)
                {
                    await ownedTransaction.CommitAsync(cancellationToken);
                }

                return;
            }

            var previousHash = await dbContext.AuditEntries
                .AsNoTracking()
                .OrderByDescending(candidate => candidate.Id)
                .Select(candidate => candidate.RowHash)
                .FirstOrDefaultAsync(cancellationToken)
                ?? AuditEntry.GenesisHash;

            dbContext.AuditEntries.Add(AuditEntry.Create(
                entry.ActorSubject,
                entry.Action,
                entry.TargetType,
                entry.TargetId,
                entry.Outcome,
                entry.OccurredAt,
                previousHash,
                entry.IdempotencyKey));
            await dbContext.SaveChangesAsync(cancellationToken);

            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

    public async Task<AuditEntryPage> QueryAsync(
        string? actor,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        long? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(actor))
        {
            query = query.Where(entry => entry.ActorSubject == actor);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(entry => entry.Action == action);
        }

        if (from.HasValue)
        {
            query = query.Where(entry => entry.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(entry => entry.OccurredAt <= to.Value);
        }

        if (cursor.HasValue)
        {
            query = query.Where(entry => entry.Id > cursor.Value);
        }

        var rows = await query
            .OrderBy(entry => entry.Id)
            .Take(limit + 1)
            .Select(entry => new AuditEntryDto(
                entry.Id,
                entry.ActorSubject,
                entry.Action,
                entry.TargetType,
                entry.TargetId,
                entry.Outcome,
                entry.OccurredAt,
                entry.RowHash,
                entry.PrevHash,
                entry.IdempotencyKey))
            .ToListAsync(cancellationToken);
        var hasNextPage = rows.Count > limit;
        var items = rows.Take(limit).ToArray();
        return new AuditEntryPage(items, hasNextPage ? items[^1].Id : null);
    }

    public async Task<AuditVerificationResult> VerifyAsync(
        long? fromId,
        long? toId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEntries.AsNoTracking().AsQueryable();
        if (fromId is > 0)
        {
            query = query.Where(entry => entry.Id >= fromId.Value);
        }

        if (toId is > 0)
        {
            query = query.Where(entry => entry.Id <= toId.Value);
        }

        var entries = await query.OrderBy(entry => entry.Id).ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return new AuditVerificationResult(true, null, null);
        }

        var expectedPreviousHash = AuditEntry.GenesisHash;
        if (entries[0].Id > 1)
        {
            expectedPreviousHash = await dbContext.AuditEntries
                .AsNoTracking()
                .Where(entry => entry.Id < entries[0].Id)
                .OrderByDescending(entry => entry.Id)
                .Select(entry => entry.RowHash)
                .FirstOrDefaultAsync(cancellationToken)
                ?? AuditEntry.GenesisHash;
        }

        foreach (var entry in entries)
        {
            if (!string.Equals(entry.PrevHash, expectedPreviousHash, StringComparison.Ordinal)
                || !string.Equals(entry.RowHash, entry.RecalculateHash(), StringComparison.Ordinal))
            {
                AuditMetrics.BrokenHashChain.Add(1);
                return new AuditVerificationResult(
                    false,
                    entry.Id,
                    "The stored hash or previous-hash link does not match the canonical audit payload.");
            }

            expectedPreviousHash = entry.RowHash;
        }

        return new AuditVerificationResult(true, null, null);
    }
}
