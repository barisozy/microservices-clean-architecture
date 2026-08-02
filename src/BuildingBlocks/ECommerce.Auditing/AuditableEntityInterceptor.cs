using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ECommerce.Auditing;

/// <summary>
/// Applies the shared BaseAuditableEntity property convention without taking a
/// compiled dependency on any service Domain assembly.
/// </summary>
public sealed class AuditableEntityInterceptor(IHttpContextAccessor httpContextAccessor)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        StampEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void StampEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var subject = httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
            ?? httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";

        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            if (!IsAuditable(entry))
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property("CreatedAt").CurrentValue = now;
                entry.Property("CreatedBy").CurrentValue = subject;
            }
            else
            {
                entry.Property("CreatedAt").IsModified = false;
                entry.Property("CreatedBy").IsModified = false;
            }

            entry.Property("LastModifiedAt").CurrentValue = now;
            entry.Property("LastModifiedBy").CurrentValue = subject;
        }
    }

    private static bool IsAuditable(EntityEntry entry) =>
        entry.Metadata.FindProperty("CreatedAt")?.ClrType == typeof(DateTimeOffset)
        && entry.Metadata.FindProperty("CreatedBy")?.ClrType == typeof(string)
        && entry.Metadata.FindProperty("LastModifiedAt")?.ClrType == typeof(DateTimeOffset)
        && entry.Metadata.FindProperty("LastModifiedBy")?.ClrType == typeof(string);
}
