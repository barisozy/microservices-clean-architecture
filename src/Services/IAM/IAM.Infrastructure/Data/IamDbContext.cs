using IAM.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace IAM.Infrastructure.Data;

public class IamDbContext : DbContext
{
    public IamDbContext(DbContextOptions<IamDbContext> options) : base(options) { }

    public DbSet<IamProfile> Profiles => Set<IamProfile>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("iam");

        modelBuilder.Entity<IamProfile>(b =>
        {
            b.HasKey(x => x.KeycloakSubject);
        });

        modelBuilder.Entity<Invitation>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.IdempotencyKey).IsUnique();
        });

        modelBuilder.Entity<GroupMembership>(b =>
        {
            b.HasKey(x => new { x.KeycloakSubject, x.GroupId });
        });

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
