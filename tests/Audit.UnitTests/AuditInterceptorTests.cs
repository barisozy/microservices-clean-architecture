using System.Security.Claims;
using ECommerce.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Audit.UnitTests;

public sealed class AuditableRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}

public sealed class NonAuditableRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
}

public sealed class AuditingTestDbContext(
    DbContextOptions<AuditingTestDbContext> options,
    AuditableEntityInterceptor interceptor) : DbContext(options)
{
    public DbSet<AuditableRecord> AuditableRecords => Set<AuditableRecord>();
    public DbSet<NonAuditableRecord> NonAuditableRecords => Set<NonAuditableRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(interceptor);
}

public sealed class AuditInterceptorTests
{
    [Fact]
    public async Task AddedEntity_ReceivesCreateAndModificationMetadata()
    {
        const string subject = "subject-123";
        var accessor = CreateAccessor(subject);
        await using var db = CreateContext(accessor);
        var entity = new AuditableRecord { Name = "created" };

        db.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.CreatedBy.ShouldBe(subject);
        entity.LastModifiedBy.ShouldBe(subject);
        entity.CreatedAt.ShouldNotBe(default);
        entity.LastModifiedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task ModifiedEntity_PreservesCreationMetadata()
    {
        var accessor = CreateAccessor("creator");
        await using var db = CreateContext(accessor);
        var entity = new AuditableRecord { Name = "created" };
        db.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var createdAt = entity.CreatedAt;

        SetSubject(accessor.HttpContext!, "modifier");
        entity.Name = "changed";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.CreatedBy.ShouldBe("creator");
        entity.CreatedAt.ShouldBe(createdAt);
        entity.LastModifiedBy.ShouldBe("modifier");
        entity.LastModifiedAt.ShouldBeGreaterThanOrEqualTo(createdAt);
    }

    [Fact]
    public async Task NonAuditableEntity_IsPersistedWithoutConventionErrors()
    {
        await using var db = CreateContext(CreateAccessor(null));
        db.Add(new NonAuditableRecord { Name = "plain" });

        await Should.NotThrowAsync(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static HttpContextAccessor CreateAccessor(string? subject)
    {
        var accessor = new HttpContextAccessor();
        var context = new DefaultHttpContext();
        accessor.HttpContext = context;
        SetSubject(context, subject);
        return accessor;
    }

    private static AuditingTestDbContext CreateContext(HttpContextAccessor accessor) =>
        new(
            new DbContextOptionsBuilder<AuditingTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new AuditableEntityInterceptor(accessor));

    private static void SetSubject(HttpContext context, string? subject)
    {
        context.User = string.IsNullOrWhiteSpace(subject)
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "Test"));
    }
}
