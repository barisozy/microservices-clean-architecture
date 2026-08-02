using Catalog.Api.Services;
using Catalog.Application;
using Catalog.Application.Common.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Data;
using ECommerce.Contracts.Events.v1;
using ECommerce.ServiceDefaults;
using MassTransit;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddKeycloakJwtAuthentication();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddGrpc();
builder.Services.AddProblemDetails();
builder.Services.AddOutputCache();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser().RequireRole("ADMIN")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
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
app.UseOutputCache();

app.MapGrpcService<CatalogGrpcService>()
    .RequireAuthorization();

app.MapGet("/api/v{version:apiVersion}/catalog/products", async (CatalogDbContext db) =>
{
    var products = await db.Products.ToListAsync();
    return Results.Ok(products);
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/api/v{version:apiVersion}/catalog/products/{sku}", async (string sku, CatalogDbContext db) =>
{
    var product = await db.Products.FirstOrDefaultAsync(p => p.Sku == sku);
    return product != null ? Results.Ok(product) : Results.NotFound();
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

var createProductEndpoint = app.MapPost("/api/v{version:apiVersion}/catalog/products", async (
    Product product,
    ClaimsPrincipal principal,
    CatalogDbContext db,
    IIamPermissionChecker permissionChecker,
    IPublishEndpoint publishEndpoint,
    CancellationToken cancellationToken) =>
{
    if (!app.Environment.IsEnvironment("Testing"))
    {
        var subject = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject)
            || !await permissionChecker.IsAllowedAsync(subject, "Catalog.Write", cancellationToken))
        {
            return Results.Forbid();
        }
    }

    if (product.Id == Guid.Empty) product.Id = Guid.CreateVersion7();
    db.Products.Add(product);
    await publishEndpoint.Publish(
        new ProductUpserted(product.Sku, product.Name, product.Price),
        cancellationToken);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/v1/catalog/products/{product.Sku}", product);
});
if (!app.Environment.IsEnvironment("Testing"))
{
    createProductEndpoint.RequireAuthorization("AdminOnly");
}

app.MapGet("/api/v{version:apiVersion}/catalog/categories", async (CatalogDbContext db) =>
{
    var categories = await db.Categories.ToListAsync();
    return Results.Ok(categories);
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/api/v{version:apiVersion}/catalog/brands", async (CatalogDbContext db) =>
{
    var brands = await db.Brands.ToListAsync();
    return Results.Ok(brands);
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/api/v{version:apiVersion}/catalog/products/{sku}/variants", async (string sku, CatalogDbContext db) =>
{
    var product = await db.Products.FirstOrDefaultAsync(p => p.Sku == sku);
    if (product == null) return Results.NotFound();
    var variants = await db.Variants.Where(v => v.ProductId == product.Id).ToListAsync();
    return Results.Ok(variants);
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/api/v{version:apiVersion}/catalog/products/{sku}/images", async (string sku, CatalogDbContext db) =>
{
    var product = await db.Products.FirstOrDefaultAsync(p => p.Sku == sku);
    if (product == null) return Results.NotFound();
    var images = await db.Images.Where(i => i.ProductId == product.Id).ToListAsync();
    return Results.Ok(images);
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.Run();

public partial class Program;
