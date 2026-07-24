using Auditing.Api.Consumers;
using Auditing.Api.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ECommerce.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();

// Configure DB
builder.Services.AddDbContext<AuditingDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("AuditingDb");
    if (builder.Environment.IsEnvironment("Testing") || string.IsNullOrEmpty(connectionString))
    {
        options.UseInMemoryDatabase("AuditingDb_InMemory");
    }
    else
    {
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.SetPostgresVersion(18, 0);
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "auditing");
        });
    }
});

// Configure MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<AuditLogCreatedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitConnectionString = builder.Configuration.GetConnectionString("rabbitmq") ?? "amqp://guest:guest@localhost:5672";
        cfg.Host(new Uri(rabbitConnectionString));
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/api/audit-logs", async (AuditingDbContext db, string? entityName, string? userId, int page = 1, int pageSize = 50) =>
{
    var query = db.AuditLogs.AsQueryable();
    if (!string.IsNullOrEmpty(entityName))
        query = query.Where(x => x.EntityName == entityName);
    if (!string.IsNullOrEmpty(userId))
        query = query.Where(x => x.UserId == userId);

    var totalCount = await query.CountAsync();
    var items = await query.OrderByDescending(x => x.Timestamp)
                           .Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .ToListAsync();

    return Results.Ok(new { totalCount, page, pageSize, items });
});

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();

public partial class Program { }
