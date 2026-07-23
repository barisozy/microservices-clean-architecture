using System.Threading.RateLimiting;
using ECommerce.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using RedisRateLimiting;
using StackExchange.Redis;
using ECommerce.Gateway.Infrastructure;
using Yarp.ReverseProxy.Forwarder;
using Microsoft.Extensions.DependencyInjection;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();

// Security: Inbound JWT Validation
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"] ?? "http://localhost:8080/realms/ecommerce";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = builder.Configuration.GetValue<bool>("Jwt:ValidateIssuer", true),
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "http://localhost:8080/realms/ecommerce",
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuthenticatedUser", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

// Valkey Client via Aspire (using StackExchange.Redis internally)
builder.AddRedisClient("valkey");

// Rate Limiting: Valkey / Sliding Window Rate Limiter (100 req/s per IP) with fallback
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("SlidingWindowIpLimiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var multiplexer = httpContext.RequestServices.GetService<IConnectionMultiplexer>();

        if (multiplexer != null && multiplexer.IsConnected)
        {
            return RedisRateLimitPartition.GetSlidingWindowRateLimiter(
                partitionKey: clientIp,
                factory: _ => new RedisSlidingWindowRateLimiterOptions
                {
                    ConnectionMultiplexerFactory = () => multiplexer,
                    PermitLimit = 100,
                    Window = TimeSpan.FromSeconds(1)
                });
        }
        else
        {
            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: clientIp,
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromSeconds(1),
                    SegmentsPerWindow = 4,
                    QueueLimit = 0
                });
        }
    });
});

// Outbound Concurrency Limiter / Bulkhead (Max 10 concurrent requests)
builder.Services.AddResiliencePipeline<string, HttpResponseMessage>("bulkhead", pipelineBuilder =>
{
    pipelineBuilder.AddConcurrencyLimiter(10, queueLimit: 0);
});

builder.Services.AddSingleton<IForwarderHttpClientFactory, ResilientForwarderHttpClientFactory>();

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapReverseProxy();

app.Run();
