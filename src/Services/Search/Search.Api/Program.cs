using System.Diagnostics;
using System.Diagnostics.Metrics;
using ECommerce.ServiceDefaults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Search.Application;
using Search.Infrastructure;
using Search.Infrastructure.Data;

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
    var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var migrationCs = config.GetConnectionString("SearchDb_Migration");
    if (!string.IsNullOrWhiteSpace(migrationCs))
    {
        db.Database.SetConnectionString(migrationCs);
    }

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

// Sprint 6: Postgres full-text tsvector/GIN ts_rank search endpoint
app.MapGet("/api/v{version:apiVersion}/search", async (
    string? q,
    ISender sender,
    IValidator<SearchQuery> validator,
    CancellationToken cancellationToken) =>
{
    var startedAt = Stopwatch.GetTimestamp();
    try
    {
    var query = new SearchQuery(q);
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid)
    {
        return Results.ValidationProblem(validation.ToDictionary());
    }
    return Results.Ok(await sender.Send(query, cancellationToken));
    }
    finally
    {
        SearchMetrics.QueryDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }
});

app.MapGet("/api/v{version:apiVersion}/search/suggest", async (
    string? q,
    ISender sender,
    IValidator<SuggestQuery> validator,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<string>());

    var query = new SuggestQuery(q);
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid)
    {
        return Results.ValidationProblem(validation.ToDictionary());
    }
    return Results.Ok(await sender.Send(query, cancellationToken));
});

app.Run();

public partial class Program;

internal static class SearchMetrics
{
    private static readonly Meter Meter = new("Search.Api");
    internal static readonly Histogram<double> QueryDuration =
        Meter.CreateHistogram<double>("search.query.duration", "ms");
}
