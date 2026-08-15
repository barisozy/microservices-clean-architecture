using ECommerce.ServiceDefaults;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Order.Api.Infrastructure;
using Order.Application;
using Order.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddKeycloakJwtAuthentication();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Order.Infrastructure.Data.OrderDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var migrationCs = config.GetConnectionString("OrderDb_Migration");
    if (!string.IsNullOrWhiteSpace(migrationCs))
    {
        db.Database.SetConnectionString(migrationCs);
    }

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

app.MapEndpoints();

app.Run();

// Enables a WebApplicationFactory test host without changing the production entry point.
public partial class Program;
