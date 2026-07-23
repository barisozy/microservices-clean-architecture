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
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuditingDb"), npgsql =>
    {
        npgsql.SetPostgresVersion(18, 0);
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "auditing");
    });
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

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();
