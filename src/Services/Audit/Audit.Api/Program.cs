using System.Security.Claims;
using Audit.Api.Services;
using Audit.Application;
using Audit.Application.AuditEntries;
using Audit.Application.Common.Interfaces;
using Audit.Infrastructure;
using ECommerce.ServiceDefaults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ComplianceAuditDbContext = Audit.Infrastructure.Data.AuditDbContext;
using LegacyAuditDbContext = Audit.Api.Data.AuditDbContext;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddKeycloakJwtAuthentication();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);
builder.Services.AddGrpc();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser().RequireRole("ADMIN")));

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<LegacyAuditDbContext>(options =>
        options.UseInMemoryDatabase("LegacyAuditLogs_Testing"));
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ComplianceAuditDbContext>();
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    await db.ApplyImmutabilityHardeningAsync(CancellationToken.None);
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

app.MapGrpcService<AuditGrpcService>()
    .RequireAuthorization("AdminOnly");

app.MapGet(
        "/api/v{version:apiVersion}/audit/entries",
        async (
            ClaimsPrincipal principal,
            ISender sender,
            IIamPermissionChecker permissionChecker,
            string? actor,
            string? action,
            DateTimeOffset? from,
            DateTimeOffset? to,
            long? cursor,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
        {
            var subject = principal.FindFirstValue("sub")
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(subject)
                || !await permissionChecker.IsAllowedAsync(subject, "Audit.Read", cancellationToken))
            {
                return Results.Forbid();
            }

            var result = await sender.Send(
                new GetAuditEntriesQuery(actor, action, from, to, cursor, limit),
                cancellationToken);
            return Results.Ok(result);
        })
    .RequireAuthorization("AdminOnly");

if (app.Environment.IsEnvironment("Testing"))
{
    // Compatibility-only read surface retained for the pre-Sprint-9 unit suite.
    app.MapGet(
        "/api/audit-logs",
        async (LegacyAuditDbContext db, string? entityName, string? userId, int page = 1, int pageSize = 50) =>
        {
            var query = db.AuditLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(entityName))
            {
                query = query.Where(entry => entry.EntityName == entityName);
            }

            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(entry => entry.UserId == userId);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(entry => entry.Timestamp)
                .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
                .Take(Math.Clamp(pageSize, 1, 100))
                .ToListAsync();
            return Results.Ok(new { totalCount, page, pageSize, items });
        });
}

app.Run();

public partial class Program;
