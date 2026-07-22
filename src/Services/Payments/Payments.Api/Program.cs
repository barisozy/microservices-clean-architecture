using ECommerce.ServiceDefaults;
using Scalar.AspNetCore;
using Payments.Api.Infrastructure;
using Payments.Application;
using Payments.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Payments.Infrastructure.Data.PaymentsDbContext>();
    db.Database.EnsureCreated();
}

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();

app.Run();
