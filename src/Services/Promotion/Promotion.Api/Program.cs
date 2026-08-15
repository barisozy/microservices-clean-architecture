using System.Security.Claims;
using ECommerce.ServiceDefaults;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Promotion.Api.Services;
using Promotion.Application;
using Promotion.Application.Common.Interfaces;
using Promotion.Infrastructure;
using Promotion.Infrastructure.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddKeycloakJwtAuthentication();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddGrpc();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("ADMIN"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var migrationCs = config.GetConnectionString("PromotionDb_Migration");
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

app.MapGrpcService<PromotionGrpcService>()
    .RequireAuthorization();

app.MapGet("/api/v{version:apiVersion}/promotion/coupons", async (
    ISender sender,
    IValidator<GetCouponsQuery> validator,
    CancellationToken cancellationToken) =>
{
    var query = new GetCouponsQuery();
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    return Results.Ok(await sender.Send(query, cancellationToken));
});

app.MapPost("/api/v{version:apiVersion}/promotion/coupons", async (
    CreateCouponRequest request,
    ClaimsPrincipal principal,
    ISender sender,
    IValidator<CreateCouponCommand> validator,
    IIamPermissionChecker permissionChecker,
    CancellationToken cancellationToken) =>
{
    if (!app.Environment.IsEnvironment("Testing"))
    {
        var subject = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject)
            || !await permissionChecker.IsAllowedAsync(subject, "Promotion.Coupon.Write", cancellationToken))
        {
            return Results.Forbid();
        }
    }

    var actor = principal.FindFirstValue("sub")
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "system";
    var command = new CreateCouponCommand(
        request.Code,
        request.DiscountType,
        request.Value,
        request.ExpiresAt,
        actor,
        !app.Environment.IsEnvironment("Testing"));
    var validation = await validator.ValidateAsync(command, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var coupon = await sender.Send(command, cancellationToken);
    return Results.Created($"/api/v1/promotion/coupons/{coupon.Id}", coupon);
}).RequireAuthorization("AdminOnly");

app.Run();

public partial class Program;

public sealed record CreateCouponRequest(string Code, string DiscountType, decimal Value, DateTime ExpiresAt);
