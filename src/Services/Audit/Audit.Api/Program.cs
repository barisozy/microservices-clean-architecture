using System.Security.Claims;
using Audit.Api.Services;
using Audit.Application;
using Audit.Application.AuditEntries;
using Audit.Application.Common.Interfaces;
using Audit.Infrastructure;
using ECommerce.ServiceDefaults;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ComplianceAuditDbContext = Audit.Infrastructure.Data.AuditDbContext;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddKeycloakJwtAuthentication();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);
builder.Services.AddGrpc();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser().RequireRole("ADMIN")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ComplianceAuditDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var migrationCs = config.GetConnectionString("AuditDb_Migration");
    if (!string.IsNullOrWhiteSpace(migrationCs))
    {
        db.Database.SetConnectionString(migrationCs);
    }

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        await db.Database.EnsureCreatedAsync();
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

app.MapGrpcService<AuditGrpcService>()
    .RequireAuthorization("AdminOnly");

app.MapGet(
        "/api/v{version:apiVersion}/audit/entries",
        async (
            ClaimsPrincipal principal,
            ISender sender,
            IValidator<GetAuditEntriesQuery> validator,
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

            var query = new GetAuditEntriesQuery(actor, action, from, to, cursor, limit);
            var validation = await validator.ValidateAsync(query, cancellationToken);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result);
        })
    .RequireAuthorization("AdminOnly");

app.Run();

public partial class Program;
