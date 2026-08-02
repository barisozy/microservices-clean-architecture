using System.Diagnostics;
using System.Diagnostics.Metrics;
using ECommerce.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Search.Application;
using Search.Infrastructure;
using Search.Infrastructure.Data;
using NpgsqlTypes;

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
app.MapGet("/api/v{version:apiVersion}/search", async (string? q, SearchDbContext db) =>
{
    var startedAt = Stopwatch.GetTimestamp();
    try
    {
    if (string.IsNullOrWhiteSpace(q))
    {
        var all = await db.SearchIndices.Take(20).ToListAsync();
        return Results.Ok(all);
    }

    var queryStr = q.Trim();
    var results = await db.SearchIndices
        .Where(s => EF.Property<NpgsqlTsVector>(s, "SearchVector").Matches(queryStr))
        .OrderByDescending(s => EF.Property<NpgsqlTsVector>(s, "SearchVector").Rank(EF.Functions.ToTsQuery(queryStr)))
        .ToListAsync();

    return Results.Ok(results);
    }
    finally
    {
        SearchMetrics.QueryDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }
});

app.MapGet("/api/v{version:apiVersion}/search/suggest", async (string? q, SearchDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<string>());

    var queryStr = q.Trim();
    var suggestions = await db.SearchIndices
        .Where(s => EF.Property<NpgsqlTsVector>(s, "SearchVector").Matches(queryStr))
        .Select(s => s.Name)
        .Take(5)
        .ToListAsync();

    return Results.Ok(suggestions);
});

app.Run();

public partial class Program;

internal static class SearchMetrics
{
    private static readonly Meter Meter = new("Search.Api");
    internal static readonly Histogram<double> QueryDuration =
        Meter.CreateHistogram<double>("search.query.duration", "ms");
}
