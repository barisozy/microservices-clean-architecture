using Customer.Application;
using Customer.Application.Common.Interfaces;
using Customer.Domain.Entities;
using Customer.Infrastructure;
using Customer.Infrastructure.Data;
using ECommerce.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Security.Claims;

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
    var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
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

var getProfileEndpoint = app.MapGet("/api/v{version:apiVersion}/customers/me", async (ClaimsPrincipal user, CustomerDbContext db) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (!Guid.TryParse(sub, out var subjectGuid)) return Results.Unauthorized();

    var profile = await db.Profiles.FirstOrDefaultAsync(p => p.KeycloakSubject == subjectGuid);
    return profile != null ? Results.Ok(profile) : Results.NotFound();
});

var updateProfileEndpoint = app.MapPut("/api/v{version:apiVersion}/customers/me", async (
    ClaimsPrincipal user,
    CustomerProfile updatedProfile,
    CustomerDbContext db,
    IIamPermissionChecker permissionChecker,
    CancellationToken cancellationToken) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (!Guid.TryParse(sub, out var subjectGuid)) return Results.Unauthorized();
    if (!app.Environment.IsEnvironment("Testing")
        && !await permissionChecker.IsAllowedAsync(sub, "Customer.Profile.Write", cancellationToken))
    {
        return Results.Forbid();
    }

    var profile = await db.Profiles.FirstOrDefaultAsync(p => p.KeycloakSubject == subjectGuid);
    if (profile == null)
    {
        updatedProfile.KeycloakSubject = subjectGuid;
        db.Profiles.Add(updatedProfile);
    }
    else
    {
        profile.DisplayName = updatedProfile.DisplayName;
        profile.Email = updatedProfile.Email;
    }

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(profile ?? updatedProfile);
});

var getAddressesEndpoint = app.MapGet("/api/v{version:apiVersion}/customers/me/addresses", async (ClaimsPrincipal user, CustomerDbContext db) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (!Guid.TryParse(sub, out var subjectGuid)) return Results.Unauthorized();

    var addresses = await db.Addresses.Where(a => a.CustomerId == subjectGuid).ToListAsync();
    return Results.Ok(addresses);
});

var createAddressEndpoint = app.MapPost("/api/v{version:apiVersion}/customers/me/addresses", async (
    ClaimsPrincipal user,
    Address address,
    CustomerDbContext db,
    IIamPermissionChecker permissionChecker,
    CancellationToken cancellationToken) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (!Guid.TryParse(sub, out var subjectGuid)) return Results.Unauthorized();
    if (!app.Environment.IsEnvironment("Testing")
        && !await permissionChecker.IsAllowedAsync(sub, "Customer.Profile.Write", cancellationToken))
    {
        return Results.Forbid();
    }

    address.CustomerId = subjectGuid;
    if (address.Id == Guid.Empty) address.Id = Guid.CreateVersion7();
    db.Addresses.Add(address);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/v1/customers/me/addresses/{address.Id}", address);
});

if (!app.Environment.IsEnvironment("Testing"))
{
    getProfileEndpoint.RequireAuthorization();
    updateProfileEndpoint.RequireAuthorization();
    getAddressesEndpoint.RequireAuthorization();
    createAddressEndpoint.RequireAuthorization();
}

app.Run();

public partial class Program;
