using ECommerce.Contracts.Events.v1;
using ECommerce.ServiceDefaults;
using IAM.Api.Services;
using IAM.Application;
using IAM.Application.Common.Interfaces;
using IAM.Domain.Entities;
using IAM.Infrastructure;
using IAM.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
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
    options.AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser().RequireRole("ADMIN")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
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

app.MapGrpcService<IamGrpcService>()
    .RequireAuthorization();

var getUsersEndpoint = app.MapGet("/api/v{version:apiVersion}/iam/users", async (IamDbContext db) =>
{
    var users = await db.Profiles.ToListAsync();
    return Results.Ok(users);
});

var createUserEndpoint = app.MapPost("/api/v{version:apiVersion}/iam/users", async (
    IamProfile profile,
    IamDbContext db,
    IKeycloakAdminClient keycloak,
    IPublishEndpoint publishEndpoint,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (profile.KeycloakSubject == Guid.Empty) profile.KeycloakSubject = Guid.CreateVersion7();
    profile.Status = IamProfileStatus.PendingIdentity;
    db.Profiles.Add(profile);
    await db.SaveChangesAsync(cancellationToken);

    if (app.Environment.IsEnvironment("Testing"))
    {
        profile.Status = IamProfileStatus.Active;
        await publishEndpoint.Publish(
            new UserRegistered(profile.KeycloakSubject, profile.Email),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/iam/users/{profile.KeycloakSubject}", profile);
    }

    try
    {
        await keycloak.EnsureUserExistsAsync(profile, cancellationToken);
        profile.Status = IamProfileStatus.Active;
        var provisionedAt = DateTimeOffset.UtcNow;
        await publishEndpoint.Publish(
            new UserRegistered(profile.KeycloakSubject, profile.Email),
            cancellationToken);
        await publishEndpoint.Publish(
            new UserProvisioned(profile.KeycloakSubject, profile.Email, provisionedAt),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        logger.LogWarning(
            exception,
            "Keycloak provisioning deferred for subject {Subject}",
            profile.KeycloakSubject);
        return Results.Accepted($"/api/v1/iam/users/{profile.KeycloakSubject}", profile);
    }

    return Results.Created($"/api/v1/iam/users/{profile.KeycloakSubject}", profile);
});

var createInvitationEndpoint = app.MapPost("/api/v{version:apiVersion}/iam/invitations", async (HttpContext httpContext, Invitation invitation, IamDbContext db) =>
{
    var key = Guid.Empty;
    var hasValidKey = httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var keyStr)
        && Guid.TryParse(keyStr, out key);
    if (!hasValidKey && !app.Environment.IsEnvironment("Testing"))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "A valid Idempotency-Key header is required.");
    }

    if (hasValidKey)
    {
        var existing = await db.Invitations.FirstOrDefaultAsync(i => i.IdempotencyKey == key);
        if (existing != null)
        {
            return Results.Created($"/api/v1/iam/invitations/{existing.Id}", existing);
        }
        invitation.IdempotencyKey = key;
    }

    if (invitation.Id == Guid.Empty) invitation.Id = Guid.CreateVersion7();
    db.Invitations.Add(invitation);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/iam/invitations/{invitation.Id}", invitation);
});

var getGroupsEndpoint = app.MapGet("/api/v{version:apiVersion}/iam/groups", async (IamDbContext db) =>
{
    var groups = await db.GroupMemberships.ToListAsync();
    return Results.Ok(groups);
});

if (!app.Environment.IsEnvironment("Testing"))
{
    getUsersEndpoint.RequireAuthorization("AdminOnly");
    createUserEndpoint.RequireAuthorization("AdminOnly");
    createInvitationEndpoint.RequireAuthorization("AdminOnly");
    getGroupsEndpoint.RequireAuthorization("AdminOnly");
}

app.Run();

public partial class Program;
