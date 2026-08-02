using ECommerce.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Notification.Application;
using Notification.Infrastructure;
using Notification.Infrastructure.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddKeycloakJwtAuthentication();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }
}

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();
app.UseProblemDetailsStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    // Operational inspection only. Notification.Api has no production REST surface.
    app.MapGet("/api/v{version:apiVersion}/notification/logs", async (NotificationDbContext db) =>
    {
        var logs = await db.Logs.OrderByDescending(l => l.SentAt).Take(50).ToListAsync();
        return Results.Ok(logs);
    });
}

app.Run();

public partial class Program;
