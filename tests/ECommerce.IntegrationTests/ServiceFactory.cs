using ECommerce.Contracts.Protos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Catalog.Infrastructure.Data;
using StackExchange.Redis;

namespace ECommerce.IntegrationTests;

/// <summary>
/// Hosts an API in-process while keeping every production dependency real.
/// </summary>
public sealed class ServiceFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private readonly InfrastructureFixture _fixture;
    private readonly Dictionary<string, string?> _extraConfig;
    private readonly OrderServiceDependencies? _orderDependencies;

    public ServiceFactory(
        InfrastructureFixture fixture,
        Dictionary<string, string?>? extraConfig = null,
        OrderServiceDependencies? orderDependencies = null)
    {
        _fixture = fixture;
        _extraConfig = extraConfig ?? [];
        _orderDependencies = orderDependencies;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var config = new Dictionary<string, string?>
        {
            ["ConnectionStrings:postgres"] = _fixture.PostgresConnectionString,
            ["ConnectionStrings:OrderDb"] = _fixture.PostgresConnectionString,
            ["ConnectionStrings:InventoryDb"] = _fixture.PostgresConnectionString,
            ["ConnectionStrings:PaymentDb"] = _fixture.PostgresConnectionString,
            ["ConnectionStrings:FulfillmentDb"] = _fixture.PostgresConnectionString,
            ["ConnectionStrings:CatalogDb"] = _fixture.PostgresConnectionString,
            ["ConnectionStrings:catalog_db"] = _fixture.PostgresConnectionString,
            ["ConnectionStrings:rabbitmq"] = _fixture.RabbitMqConnectionString,
            ["ConnectionStrings:valkey"] = _fixture.ValkeyConnectionString,
            ["Jwt:Authority"] = _fixture.KeycloakAuthority,
            ["Jwt:Issuer"] = string.IsNullOrWhiteSpace(_fixture.KeycloakIssuer)
                ? _fixture.KeycloakAuthority
                : _fixture.KeycloakIssuer,
            ["Jwt:ValidateIssuer"] = "true",
            ["Jwt:RequireHttpsMetadata"] = "false",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
            ["OTEL_SDK_DISABLED"] = "true",
            ["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "true"
        };

        foreach (var (key, value) in _extraConfig)
            config[key] = value;

        // EnsureCreated only initializes the first DbContext in a shared database.
        // Integration hosts therefore follow the production migration path so every
        // service creates its own schema and MassTransit inbox/outbox tables.
        builder.UseEnvironment("IntegrationTesting");
        builder.ConfigureLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(config));
        builder.ConfigureServices(services =>
        {
            if (typeof(TProgram) == typeof(Catalog.Api.ICatalogApiMarker))
            {
                services.RemoveAll<CatalogDbContext>();
                services.RemoveAll<DbContextOptions<CatalogDbContext>>();
                services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(
                    _fixture.PostgresConnectionString,
                    npgsql =>
                    {
                        npgsql.SetPostgresVersion(18, 0);
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog");
                    }));
            }

            services.RemoveAll<IExceptionHandler>();
            services.AddSingleton<IExceptionHandler, TestExceptionHandler>();

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = _fixture.KeycloakIssuer };
                foreach (var signingKey in _fixture.KeycloakSigningKeys)
                    configuration.SigningKeys.Add(signingKey);

                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                options.TokenValidationParameters.ValidIssuer = _fixture.KeycloakIssuer;
                options.TokenValidationParameters.IssuerSigningKeys = _fixture.KeycloakSigningKeys;
                options.Events.OnAuthenticationFailed = context =>
                {
                    context.Response.Headers["X-Integration-Authentication-Failure"] = context.Exception.Message;
                    return Task.CompletedTask;
                };
            });

            if (_orderDependencies is not null)
            {
                services.RemoveAll<InventoryService.InventoryServiceClient>();
                services.AddGrpcClient<InventoryService.InventoryServiceClient>(options =>
                        options.Address = new Uri("http://inventory.integration.test"))
                    .ConfigurePrimaryHttpMessageHandler(_ => _orderDependencies.CreateInventoryHandler());

                services.RemoveAll<CatalogService.CatalogServiceClient>();
                services.AddGrpcClient<CatalogService.CatalogServiceClient>(options =>
                        options.Address = new Uri("http://catalog.integration.test"))
                    .ConfigurePrimaryHttpMessageHandler(_ => _orderDependencies.CreateCatalogHandler());
            }

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(_fixture.ValkeyConnectionString));
        });
    }
}

internal sealed class TestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsync(exception.ToString(), cancellationToken);
        return true;
    }
}

public sealed class OrderServiceDependencies(
    WebApplicationFactory<Inventory.Api.IInventoryApiMarker> inventoryFactory,
    WebApplicationFactory<Catalog.Api.ICatalogApiMarker> catalogFactory,
    string accessToken)
{
    public HttpMessageHandler CreateInventoryHandler() =>
        new BearerTokenHandler(inventoryFactory.Server.CreateHandler(), accessToken);

    public HttpMessageHandler CreateCatalogHandler() =>
        new BearerTokenHandler(catalogFactory.Server.CreateHandler(), accessToken);

    private sealed class BearerTokenHandler(HttpMessageHandler innerHandler, string token) : DelegatingHandler(innerHandler)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
