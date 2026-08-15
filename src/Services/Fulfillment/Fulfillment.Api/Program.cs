using ECommerce.ServiceDefaults;
using Fulfillment.Application;
using Fulfillment.Application.Common.Interfaces;
using Fulfillment.Application.Shipments;
using Fulfillment.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

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
    var db = scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var migrationCs = config.GetConnectionString("FulfillmentDb_Migration");
    if (!string.IsNullOrWhiteSpace(migrationCs))
    {
        db.Database.SetConnectionString(migrationCs);
    }

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing") || app.Environment.IsEnvironment("IntegrationTesting"))
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

app.MapGet("/api/v{version:apiVersion}/fulfillment/shipments/{orderId:guid}", async (
    Guid orderId,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var shipment = await sender.Send(new GetShipmentQuery(orderId), cancellationToken);
    return shipment != null ? Results.Ok(shipment) : Results.NotFound();
}).RequireAuthorization();

app.Run();
