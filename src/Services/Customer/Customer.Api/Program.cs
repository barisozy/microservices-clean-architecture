using Customer.Application;
using Customer.Application.Common.Interfaces;
using Customer.Infrastructure;
using Customer.Infrastructure.Data;
using ECommerce.ServiceDefaults;
using FluentValidation;
using MediatR;
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
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var migrationCs = config.GetConnectionString("CustomerDb_Migration");
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

var getProfileEndpoint = app.MapGet("/api/v{version:apiVersion}/customers/me", async (
    ClaimsPrincipal user,
    ISender sender,
    IValidator<GetProfileQuery> validator,
    CancellationToken cancellationToken) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (!Guid.TryParse(sub, out var subjectGuid)) return Results.Unauthorized();

    var query = new GetProfileQuery(subjectGuid);
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var profile = await sender.Send(query, cancellationToken);
    return profile != null ? Results.Ok(profile) : Results.NotFound();
});

var updateProfileEndpoint = app.MapPut("/api/v{version:apiVersion}/customers/me", async (
    ClaimsPrincipal user,
    UpdateProfileRequest request,
    ISender sender,
    IValidator<UpdateProfileCommand> validator,
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

    var command = new UpdateProfileCommand(subjectGuid, request.DisplayName, request.Email);
    var validation = await validator.ValidateAsync(command, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    return Results.Ok(await sender.Send(command, cancellationToken));
});

var getAddressesEndpoint = app.MapGet("/api/v{version:apiVersion}/customers/me/addresses", async (
    ClaimsPrincipal user,
    ISender sender,
    IValidator<GetAddressesQuery> validator,
    CancellationToken cancellationToken) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (!Guid.TryParse(sub, out var subjectGuid)) return Results.Unauthorized();

    var query = new GetAddressesQuery(subjectGuid);
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    return Results.Ok(await sender.Send(query, cancellationToken));
});

var createAddressEndpoint = app.MapPost("/api/v{version:apiVersion}/customers/me/addresses", async (
    ClaimsPrincipal user,
    CreateAddressRequest request,
    ISender sender,
    IValidator<CreateAddressCommand> validator,
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

    var command = new CreateAddressCommand(subjectGuid, request.Line1, request.City, request.PostalCode);
    var validation = await validator.ValidateAsync(command, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var address = await sender.Send(command, cancellationToken);
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

public sealed record UpdateProfileRequest(string DisplayName, string Email);
public sealed record CreateAddressRequest(string Line1, string City, string PostalCode);
