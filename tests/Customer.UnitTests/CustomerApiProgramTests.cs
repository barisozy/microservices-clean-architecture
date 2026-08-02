using System.Net;
using System.Net.Http.Json;
using Customer.Domain.Entities;
using Customer.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;


namespace Customer.UnitTests;

public sealed class CustomerApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CustomerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CustomerDbContext>>();
            services.AddDbContext<CustomerDbContext>(options => options.UseInMemoryDatabase("customer-api-tests"));
            services.RemoveAll<IHostedService>();
        });
    }
}

public sealed class CustomerApiProgramTests : IClassFixture<CustomerApiFactory>
{
    private readonly CustomerApiFactory _factory;
    public CustomerApiProgramTests(CustomerApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ProfileAndAddressEndpoints_RejectRequestsWithoutValidSubject()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/v1/customers/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/customers/me/addresses", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedCustomer_CanReadAndUpdateProfileAndAddresses()
    {
        var subject = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("CustomerTestScheme")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, CustomerTestAuthHandler>("CustomerTestScheme", _ => { });
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("Test-Sub", subject.ToString());

        var notFound = await client.GetAsync("/api/v1/customers/me", TestContext.Current.CancellationToken);
        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var putProfile = await client.PutAsJsonAsync("/api/v1/customers/me", new CustomerProfile { DisplayName = "Alice", Email = "alice@example.com" }, TestContext.Current.CancellationToken);
        putProfile.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updateProfile = await client.PutAsJsonAsync("/api/v1/customers/me", new CustomerProfile { DisplayName = "Alice Smith", Email = "alice.smith@example.com" }, TestContext.Current.CancellationToken);
        updateProfile.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getProfile = await client.GetAsync("/api/v1/customers/me", TestContext.Current.CancellationToken);
        getProfile.StatusCode.ShouldBe(HttpStatusCode.OK);

        var addAddress = await client.PostAsJsonAsync("/api/v1/customers/me/addresses", new Address { Line1 = "123 Main St", City = "Metropolis", PostalCode = "12345" }, TestContext.Current.CancellationToken);
        addAddress.StatusCode.ShouldBe(HttpStatusCode.Created);


        var getAddresses = await client.GetAsync("/api/v1/customers/me/addresses", TestContext.Current.CancellationToken);
        getAddresses.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

public sealed class CustomerTestAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public CustomerTestAuthHandler(Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var subHeader = Context.Request.Headers["Test-Sub"].FirstOrDefault();
        if (string.IsNullOrEmpty(subHeader)) return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Fail("No Sub"));
        var identity = new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("sub", subHeader) }, "CustomerTestScheme");
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(new Microsoft.AspNetCore.Authentication.AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), "CustomerTestScheme")));
    }
}

public class CustomerDevelopmentApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CustomerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CustomerDbContext>>();
            services.AddDbContext<CustomerDbContext>(options => options.UseInMemoryDatabase("customer-dev-tests"));
            services.RemoveAll<IHostedService>();
        });
    }
}

public sealed class CustomerDevelopmentProgramTests : IClassFixture<CustomerDevelopmentApiFactory>
{
    private readonly CustomerDevelopmentApiFactory _factory;
    public CustomerDevelopmentProgramTests(CustomerDevelopmentApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DevelopmentHost_ExposesOpenApiAndScalarEndpoints()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var scalar = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);
        scalar.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently);
    }
}

