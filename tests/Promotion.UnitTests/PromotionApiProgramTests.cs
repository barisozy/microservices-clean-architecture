using System.Net;
using Promotion.Domain.Entities;
using Promotion.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace Promotion.UnitTests;

public sealed class PromotionApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PromotionDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PromotionDbContext>>();
            services.AddDbContext<PromotionDbContext>(options => options.UseInMemoryDatabase("promotion-api-tests"));
            services.RemoveAll<IHostedService>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.AuthenticationScheme, _ => { });
        });
    }
}

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "PromotionTest";

    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "ADMIN") }, AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), AuthenticationScheme)));
    }
}

public sealed class PromotionApiProgramTests : IClassFixture<PromotionApiFactory>
{
    private readonly PromotionApiFactory _factory;
    public PromotionApiProgramTests(PromotionApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CouponListEndpoint_ReturnsPersistedCoupons()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
            db.Coupons.RemoveRange(db.Coupons);
            db.Coupons.Add(new Coupon { Id = Guid.NewGuid(), Code = "API10", Value = 10, DiscountType = "PERCENTAGE", ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _factory.CreateClient().GetAsync("/api/v1/promotion/coupons", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("API10");
    }

    [Fact]
    public async Task CreateCouponEndpoint_AssignsIdAndPersistsCouponForAdmin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/promotion/coupons")
        {
            Content = JsonContent.Create(new Coupon
            {
                Code = "NEW20",
                DiscountType = "FIXED",
                Value = 20,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            })
        };

        var response = await _factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldStartWith("/api/v1/promotion/coupons/");
        using var scope = _factory.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<PromotionDbContext>().Coupons.SingleAsync(x => x.Code == "NEW20", TestContext.Current.CancellationToken);
        created.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateCouponEndpoint_PreservesExistingId()
    {
        var existingId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/promotion/coupons")
        {
            Content = JsonContent.Create(new Coupon
            {
                Id = existingId,
                Code = "PRESERVED30",
                DiscountType = "FIXED",
                Value = 30,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            })
        };

        var response = await _factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<PromotionDbContext>().Coupons.SingleAsync(x => x.Code == "PRESERVED30", TestContext.Current.CancellationToken);
        created.Id.ShouldBe(existingId);
    }
}


public sealed class PromotionDevelopmentApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PromotionDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PromotionDbContext>>();
            services.AddDbContext<PromotionDbContext>(options => options.UseInMemoryDatabase("promotion-dev-tests"));
            services.RemoveAll<IHostedService>();
        });
    }
}

public sealed class PromotionDevelopmentProgramTests : IClassFixture<PromotionDevelopmentApiFactory>
{
    private readonly PromotionDevelopmentApiFactory _factory;
    public PromotionDevelopmentProgramTests(PromotionDevelopmentApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DevelopmentHost_ExposesOpenApiAndScalarEndpoints()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var scalar = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);
        scalar.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently);
    }
}
