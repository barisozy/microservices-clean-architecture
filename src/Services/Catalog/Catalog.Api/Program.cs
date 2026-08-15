using Catalog.Api.Services;
using Catalog.Application;
using Catalog.Application.Common.Interfaces;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Data;
using ECommerce.ServiceDefaults;
using FluentValidation;
using MediatR;
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
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var migrationCs = config.GetConnectionString("CatalogDb_Migration");
    if (!string.IsNullOrWhiteSpace(migrationCs))
    {
        db.Database.SetConnectionString(migrationCs);
    }

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        await db.Database.EnsureCreatedAsync();
    }
    else if (app.Environment.IsEnvironment("IntegrationTesting"))
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

app.MapGet("/api/v{version:apiVersion}/catalog/products", async (
    ISender sender,
    IValidator<GetProductsQuery> validator,
    CancellationToken cancellationToken) =>
{
    var query = new GetProductsQuery();
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    return Results.Ok(await sender.Send(query, cancellationToken));
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/api/v{version:apiVersion}/catalog/products/{sku}", async (
    string sku,
    ISender sender,
    IValidator<GetProductQuery> validator,
    CancellationToken cancellationToken) =>
{
    var query = new GetProductQuery(sku);
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var product = await sender.Send(query, cancellationToken);
    return product != null ? Results.Ok(product) : Results.NotFound();
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

var createProductEndpoint = app.MapPost("/api/v{version:apiVersion}/catalog/products", async (
    CreateProductRequest request,
    ClaimsPrincipal principal,
    ISender sender,
    IValidator<CreateProductCommand> validator,
    IIamPermissionChecker permissionChecker,
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

    var command = new CreateProductCommand(
        request.Sku,
        request.Name,
        request.Description,
        request.Price,
        request.BrandId,
        request.CategoryId);
    var validation = await validator.ValidateAsync(command, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var product = await sender.Send(command, cancellationToken);
    return Results.Created($"/api/v1/catalog/products/{product.Sku}", product);
});
if (!app.Environment.IsEnvironment("Testing"))
{
    createProductEndpoint.RequireAuthorization("AdminOnly");
}

app.MapGet("/api/v{version:apiVersion}/catalog/categories", async (
    ISender sender,
    IValidator<GetCategoriesQuery> validator,
    CancellationToken cancellationToken) =>
{
    var query = new GetCategoriesQuery();
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    return Results.Ok(await sender.Send(query, cancellationToken));
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/api/v{version:apiVersion}/catalog/brands", async (
    ISender sender,
    IValidator<GetBrandsQuery> validator,
    CancellationToken cancellationToken) =>
{
    var query = new GetBrandsQuery();
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    return Results.Ok(await sender.Send(query, cancellationToken));
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/api/v{version:apiVersion}/catalog/products/{sku}/variants", async (
    string sku,
    ISender sender,
    IValidator<GetVariantsQuery> validator,
    CancellationToken cancellationToken) =>
{
    var query = new GetVariantsQuery(sku);
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var variants = await sender.Send(query, cancellationToken);
    if (variants is null) return Results.NotFound();
    return Results.Ok(variants);
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.MapGet("/api/v{version:apiVersion}/catalog/products/{sku}/images", async (
    string sku,
    ISender sender,
    IValidator<GetImagesQuery> validator,
    CancellationToken cancellationToken) =>
{
    var query = new GetImagesQuery(sku);
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var images = await sender.Send(query, cancellationToken);
    if (images is null) return Results.NotFound();
    return Results.Ok(images);
}).CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)));

app.Run();

public partial class Program;

public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string Description,
    decimal Price,
    Guid BrandId,
    Guid CategoryId);
