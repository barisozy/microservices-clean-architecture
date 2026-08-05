using System.Security.Claims;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Microsoft.IdentityModel.Tokens;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using ECommerce.ServiceDefaults.Observability;
using Serilog;
using Serilog.Formatting.Json;

namespace ECommerce.ServiceDefaults;

public static class Extensions
{
    public static TBuilder AddBasicServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.With(new ActivityEnricher())
            .WriteTo.Console(new JsonFormatter()));

        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            });
        });

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddProblemDetails();
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<ECommerce.ServiceDefaults.Interceptors.GrpcJwtHeaderInterceptor>();
        builder.Services.AddTransient<ECommerce.ServiceDefaults.Interceptors.GrpcTraceContextInterceptor>();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Production-Ready Strict Resilience Pipeline
            http.AddStandardResilienceHandler(options =>
            {
                // Jitter and Exponential Backoff
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // Radical Skepticism: Strict Circuit Breaker
                options.CircuitBreaker.FailureRatio = 0.5; // Break on 50% failures
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

                // Aggressive Timeouts (Avoid Thundering Herd)
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
            });
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder AddKeycloakJwtAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var authority = builder.Configuration["Jwt:Authority"]
            ?? builder.Configuration["Keycloak:Authority"]
            ?? "http://localhost:8080/realms/ecommerce";
        var validateIssuer = builder.Configuration.GetValue("Jwt:ValidateIssuer", true);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = builder.Configuration.GetValue("Jwt:RequireHttpsMetadata", false);
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = validateIssuer,
                    ValidIssuer = validateIssuer ? builder.Configuration["Jwt:Issuer"] ?? authority : null,
                    ValidateAudience = false,
                    NameClaimType = "sub",
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is not ClaimsIdentity identity)
                        {
                            return Task.CompletedTask;
                        }

                        var realmAccess = context.Principal.FindFirst("realm_access")?.Value;
                        if (string.IsNullOrWhiteSpace(realmAccess))
                        {
                            return Task.CompletedTask;
                        }

                        try
                        {
                            using var document = JsonDocument.Parse(realmAccess);
                            if (!document.RootElement.TryGetProperty("roles", out var roles))
                            {
                                return Task.CompletedTask;
                            }

                            foreach (var role in roles.EnumerateArray())
                            {
                                var value = role.GetString();
                                if (!string.IsNullOrWhiteSpace(value) && !identity.HasClaim(ClaimTypes.Role, value))
                                {
                                    identity.AddClaim(new Claim(ClaimTypes.Role, value));
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            context.Fail("The realm_access claim is malformed.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });
        builder.Services.AddAuthorization();

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(
                        "ECommerce.Order.Checkout",
                        "MassTransit",
                        "Npgsql",
                        "Order.Api",
                        "Inventory.Api",
                        "Fulfillment.Api",
                        "IAM.Api",
                        "Catalog.Api",
                        "Customer.Api",
                        "Search.Api",
                        "Notification.Api",
                        "Promotion.Api",
                        "Audit.Api");
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddSource("MassTransit", "Npgsql");
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }

    public static WebApplication UseProblemDetailsStatusCodePages(this WebApplication app)
    {
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var httpContext = statusCodeContext.HttpContext;
            if (httpContext.Response.HasStarted)
            {
                return;
            }

            var service = httpContext.RequestServices.GetService<IProblemDetailsService>();
            if (service is null)
            {
                return;
            }

            await service.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = httpContext.Response.StatusCode,
                    Title = ReasonPhrases.GetReasonPhrase(httpContext.Response.StatusCode)
                }
            });
        });

        return app;
    }
}
