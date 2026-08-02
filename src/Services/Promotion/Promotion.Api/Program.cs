using System.Security.Claims;
using ECommerce.Contracts.Events.v1;
using ECommerce.ServiceDefaults;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Promotion.Api.Services;
using Promotion.Application;
using Promotion.Application.Common.Interfaces;
using Promotion.Domain.Entities;
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

app.MapGrpcService<PromotionGrpcService>()
    .RequireAuthorization();

app.MapGet("/api/v{version:apiVersion}/promotion/coupons", async (PromotionDbContext db) =>
{
    var coupons = await db.Coupons.ToListAsync();
    return Results.Ok(coupons);
});

app.MapPost("/api/v{version:apiVersion}/promotion/coupons", async (
    Coupon coupon,
    ClaimsPrincipal principal,
    PromotionDbContext db,
    IIamPermissionChecker permissionChecker,
    IPublishEndpoint publishEndpoint,
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

    if (string.IsNullOrWhiteSpace(coupon.Code) || coupon.Value <= 0)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Coupon code and a positive value are required.");
    }

    if (coupon.ExpiresAt <= DateTime.UtcNow)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Expired coupons cannot be created.");
    }

    if (coupon.Id == Guid.Empty) coupon.Id = Guid.CreateVersion7();
    db.Coupons.Add(coupon);
    if (!app.Environment.IsEnvironment("Testing"))
    {
        var actor = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";
        await publishEndpoint.Publish(
            new CouponWritten(actor, coupon.Code, "Created", DateTimeOffset.UtcNow),
            cancellationToken);
    }

    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/v1/promotion/coupons/{coupon.Id}", coupon);
}).RequireAuthorization("AdminOnly");

app.Run();

public partial class Program;
