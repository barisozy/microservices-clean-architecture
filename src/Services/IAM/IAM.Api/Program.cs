using ECommerce.ServiceDefaults;
using FluentValidation;
using IAM.Api.Services;
using IAM.Application;
using IAM.Application.Common.Interfaces;
using IAM.Infrastructure;
using IAM.Infrastructure.Data;
using MediatR;
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
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var migrationCs = config.GetConnectionString("IamDb_Migration");
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

app.MapGrpcService<IamGrpcService>()
    .RequireAuthorization();

var getUsersEndpoint = app.MapGet("/api/v{version:apiVersion}/iam/users", async (
    ISender sender,
    IValidator<GetUsersQuery> validator,
    CancellationToken cancellationToken) =>
{
    var query = new GetUsersQuery();
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    return Results.Ok(await sender.Send(query, cancellationToken));
});

var createUserEndpoint = app.MapPost("/api/v{version:apiVersion}/iam/users", async (
    CreateUserRequest request,
    ISender sender,
    IValidator<CreateUserCommand> validator,
    CancellationToken cancellationToken) =>
{
    var command = new CreateUserCommand(
        request.KeycloakSubject == Guid.Empty ? Guid.CreateVersion7() : request.KeycloakSubject,
        request.DisplayName,
        request.Email,
        request.Role.ToUpperInvariant(),
        app.Environment.IsEnvironment("Testing"));
    var validation = await validator.ValidateAsync(command, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var result = await sender.Send(command, cancellationToken);
    var location = $"/api/v1/iam/users/{result.Profile.KeycloakSubject}";
    return result.Accepted ? Results.Accepted(location, result.Profile) : Results.Created(location, result.Profile);
});

var createInvitationEndpoint = app.MapPost("/api/v{version:apiVersion}/iam/invitations", async (
    HttpContext httpContext,
    CreateInvitationRequest request,
    ISender sender,
    IValidator<CreateInvitationCommand> validator,
    CancellationToken cancellationToken) =>
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

    if (!hasValidKey) key = Guid.CreateVersion7();
    var command = new CreateInvitationCommand(key, request.Email, request.Role.ToUpperInvariant(), request.ExpiresAt);
    var validation = await validator.ValidateAsync(command, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var invitation = await sender.Send(command, cancellationToken);
    return Results.Created($"/api/v1/iam/invitations/{invitation.Id}", invitation);
});

var getGroupsEndpoint = app.MapGet("/api/v{version:apiVersion}/iam/groups", async (
    ISender sender,
    IValidator<GetGroupsQuery> validator,
    CancellationToken cancellationToken) =>
{
    var query = new GetGroupsQuery();
    var validation = await validator.ValidateAsync(query, cancellationToken);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    return Results.Ok(await sender.Send(query, cancellationToken));
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

public sealed record CreateUserRequest(Guid KeycloakSubject, string DisplayName, string Email, string Role);
public sealed record CreateInvitationRequest(string Email, string Role, DateTime ExpiresAt);
