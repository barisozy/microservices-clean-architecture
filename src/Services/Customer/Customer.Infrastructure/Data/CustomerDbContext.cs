using Customer.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Customer.Infrastructure.Data;

public class CustomerDbContext : DbContext
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options) : base(options) { }

    public DbSet<CustomerProfile> Profiles => Set<CustomerProfile>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<CustomerPreference> Preferences => Set<CustomerPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("customer");

        modelBuilder.Entity<CustomerProfile>(b => b.HasKey(x => x.KeycloakSubject));
        modelBuilder.Entity<Address>(b => b.HasKey(x => x.Id));
        modelBuilder.Entity<CustomerPreference>(b => b.HasKey(x => x.Id));

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
