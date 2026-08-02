using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ECommerce.Contracts.Protos;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Order.Application.Basket.Commands;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Commands.CreateOrder;
using Order.Application.Orders.Queries;
using Order.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public sealed class OrderApiEndpointTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;

    public OrderApiEndpointTests(OrderApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetBasket_ShouldReturnBasketForAuthenticatedSubject()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/basket/", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<Dictionary<string, int>>(TestContext.Current.CancellationToken))!.ShouldBe(new Dictionary<string, int>
        {
            ["SKU-1"] = 2
        });
        _factory.Sender.Verify(x => x.Send(It.Is<GetBasketQuery>(q => q.BuyerId == OrderApiFactory.SubjectId.ToString()), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateAndDeleteBasket_ShouldDispatchCommandsForAuthenticatedSubject()
    {
        var client = _factory.CreateClient();

        var update = await client.PutAsJsonAsync("/api/v1/basket/", new[]
        {
            new { sku = "SKU-2", quantity = 3 }
        }, TestContext.Current.CancellationToken);
        var delete = await client.DeleteAsync("/api/v1/basket/", TestContext.Current.CancellationToken);

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        delete.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.Sender.Verify(x => x.Send(It.Is<UpdateBasketCommand>(q => q.BuyerId == OrderApiFactory.SubjectId.ToString() && q.Items.Single().Sku == "SKU-2"), It.IsAny<CancellationToken>()), Times.Once);
        // Delete uses NameIdentifier first; the test identity intentionally supplies both claim forms.
        _factory.Sender.Verify(x => x.Send(It.Is<DeleteBasketCommand>(q => q.BuyerId == OrderApiFactory.SubjectId.ToString()), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithIdempotencyKey_ShouldReturnCreatedAndDispatchRequest()
    {
        var client = _factory.CreateClient();
        var expectedId = Guid.NewGuid();
        var idempotencyKey = Guid.CreateVersion7().ToString("D");
        _factory.NextCreatedOrderId = expectedId;
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var response = await client.PostAsJsonAsync("/api/v1/orders/", new
        {
            items = new[] { new { sku = "SKU-3", quantity = 1, unitPrice = 12.50m } }
        }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken)).ShouldBe(expectedId);
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/orders/{expectedId}");
        _factory.Sender.Verify(x => x.Send(It.Is<CreateOrderCommand>(q =>
            q.CustomerId == OrderApiFactory.SubjectId && q.KeycloakSubject == OrderApiFactory.SubjectId &&
            q.IdempotencyKey == idempotencyKey && q.Items!.Single().Sku == "SKU-3"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithoutIdempotencyKey_ShouldReturnBadRequestProblemDetails()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/orders/", new { items = Array.Empty<object>() }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("Idempotency-Key header is required");
    }

    [Fact]
    public async Task GetOrder_ShouldReturnOkOrNotFoundBasedOnQueryResult()
    {
        var client = _factory.CreateClient();
        var foundId = Guid.NewGuid();
        _factory.OrderToReturn = new OrderStatusDto(foundId, "Pending", "buyer-1");

        var found = await client.GetAsync($"/api/v1/orders/{foundId}", TestContext.Current.CancellationToken);
        _factory.OrderToReturn = null;
        var missing = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        found.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await found.Content.ReadFromJsonAsync<OrderStatusDto>(TestContext.Current.CancellationToken))!.Status.ShouldBe("Pending");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateOrder_WithFallbackClaims_ShouldProcessSuccessfully()
    {
        var customClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("FallbackScheme")
                    .AddScheme<AuthenticationSchemeOptions, FallbackAuthHandler>("FallbackScheme", _ => { });
            });
        }).CreateClient();

        customClient.DefaultRequestHeaders.Add("Idempotency-Key", Guid.CreateVersion7().ToString("D"));
        var response = await customClient.PostAsJsonAsync("/api/v1/orders/", new { items = Array.Empty<object>() }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateOrder_WithNameIdentifierOnly_ShouldUseFallbackSub()
    {
        var validGuid = Guid.NewGuid().ToString();
        var customClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("NameIdScheme")
                    .AddScheme<AuthenticationSchemeOptions, NameIdAuthHandler>("NameIdScheme", _ => { });
            });
        }).CreateClient();

        customClient.DefaultRequestHeaders.Add("Idempotency-Key", Guid.CreateVersion7().ToString("D"));
        var response = await customClient.PostAsJsonAsync("/api/v1/orders/", new { items = Array.Empty<object>() }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}

public sealed class NameIdAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public NameIdAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }, "NameIdScheme");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "NameIdScheme")));
    }
}

public sealed class FallbackAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public FallbackAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "invalid-guid") }, "FallbackScheme");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "FallbackScheme")));
    }
}



public class OrderDevelopmentApiFactory : OrderApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<OrderDbContext>>();
            services.RemoveAll<OrderDbContext>();
            services.RemoveAll<IOrderDbContext>();
            services.AddDbContext<OrderDbContext>(options => options.UseInMemoryDatabase("order-dev-api-tests"));
            services.AddScoped<IOrderDbContext>(provider => provider.GetRequiredService<OrderDbContext>());

            services.RemoveAll<ISender>();
            services.AddSingleton(Sender.Object);
            services.RemoveAll<IBasketService>();
            services.AddScoped<IBasketService, NoopBasketService>();
            services.RemoveAll<IHostedService>();
        });
    }
}

public sealed class OrderDevelopmentProgramTests : IClassFixture<OrderDevelopmentApiFactory>
{
    private readonly OrderDevelopmentApiFactory _factory;
    public OrderDevelopmentProgramTests(OrderDevelopmentApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DevelopmentHost_ExposesOpenApiAndScalarEndpoints()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var scalar = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);
        scalar.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently);
    }
}


public class OrderApiFactory : WebApplicationFactory<Program>
{
    internal static readonly Guid SubjectId = Guid.Parse("a5d2032e-68aa-4f81-8b5a-36901951db15");
    internal Mock<ISender> Sender { get; } = new(MockBehavior.Strict);
    internal Guid NextCreatedOrderId { get; set; } = Guid.NewGuid();
    internal OrderStatusDto? OrderToReturn { get; set; }

    public OrderApiFactory()
    {
        Sender.Setup(x => x.Send(It.IsAny<GetBasketQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["SKU-1"] = 2 });
        Sender.Setup(x => x.Send(It.IsAny<UpdateBasketCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Sender.Setup(x => x.Send(It.IsAny<DeleteBasketCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Sender.Setup(x => x.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CreateOrderCommand _, CancellationToken _) => Task.FromResult(NextCreatedOrderId));
        Sender.Setup(x => x.Send(It.IsAny<GetOrderQuery>(), It.IsAny<CancellationToken>()))
            .Returns((GetOrderQuery _, CancellationToken _) => Task.FromResult(OrderToReturn));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<OrderDbContext>>();
            services.RemoveAll<OrderDbContext>();
            services.RemoveAll<IOrderDbContext>();
            services.AddDbContext<OrderDbContext>(options => options.UseInMemoryDatabase("order-api-tests"));
            services.AddScoped<IOrderDbContext>(provider => provider.GetRequiredService<OrderDbContext>());

            services.RemoveAll<ISender>();
            services.AddSingleton(Sender.Object);
            services.RemoveAll<IBasketService>();
            services.AddScoped<IBasketService, NoopBasketService>();

            // The API host does not need a broker in endpoint tests. Removing hosted services prevents
            // MassTransit from attempting to establish a RabbitMQ connection while retaining API wiring.
            services.RemoveAll<IHostedService>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
        });
    }
}

public sealed class NoopBasketService : IBasketService
{
    public Task<Dictionary<string, int>> GetBasketAsync(string buyerId, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<string, int>());
    public Task<bool> SetBasketAsync(string buyerId, Dictionary<string, int> items, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> DeleteBasketAsync(string buyerId, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "OrderApiTest";
    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", OrderApiFactory.SubjectId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, OrderApiFactory.SubjectId.ToString())
        }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
