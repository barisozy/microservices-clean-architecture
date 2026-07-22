using ECommerce.ServiceDefaults;
using Fulfillment.Application;
using Fulfillment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Fulfillment.Infrastructure.FulfillmentDbContext>();
    db.Database.EnsureCreated();
}

app.MapDefaultEndpoints();

app.Run();
