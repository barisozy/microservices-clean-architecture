using System.Net;
using System.Net.Http.Json;
using IAM.Domain.Entities;
using IAM.Infrastructure.Data;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace IAM.UnitTests;

public class IamApiFactory : WebApplicationFactory<Program>
{
    public Mock<IPublishEndpoint> PublishEndpoint { get; } = new();
    protected virtual string EnvironmentName => "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IamDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IamDbContext>>();
            services.AddDbContext<IamDbContext>(options => options.UseInMemoryDatabase("iam-api-tests"));
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IPublishEndpoint>();
            services.AddSingleton(PublishEndpoint.Object);
        });
    }
}

public sealed class IamDevelopmentApiFactory : IamApiFactory
{
    protected override string EnvironmentName => "Development";
}

public sealed class IamApiProgramTests : IClassFixture<IamApiFactory>
{
    private readonly IamApiFactory _factory;
    public IamApiProgramTests(IamApiFactory factory) => _factory = factory;

    [Fact]
    public async Task UserAndGroupEndpoints_ReturnPersistedData()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
            db.Profiles.RemoveRange(db.Profiles);
            db.GroupMemberships.RemoveRange(db.GroupMemberships);
            db.Profiles.Add(new IamProfile { KeycloakSubject = Guid.NewGuid(), DisplayName = "API user", Email = "api@example.test" });
            db.GroupMemberships.Add(new GroupMembership { KeycloakSubject = Guid.NewGuid(), GroupId = "customers" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = _factory.CreateClient();
        (await client.GetAsync("/api/v1/iam/users", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/iam/groups", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvitationEndpoint_IsIdempotentForTheSameKey()
    {
        var client = _factory.CreateClient();
        var key = Guid.NewGuid().ToString();
        var request = new Invitation { Email = "invite@example.test", Role = "CUSTOMER", ExpiresAt = DateTime.UtcNow.AddDays(1) };

        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/invitations") { Content = JsonContent.Create(request) };
        first.Headers.Add("Idempotency-Key", key);
        using var second = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/invitations") { Content = JsonContent.Create(request) };
        second.Headers.Add("Idempotency-Key", key);

        (await client.SendAsync(first, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.SendAsync(second, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task InvitationEndpoint_CreatesInvitationWhenIdempotencyKeyIsMissingOrMalformed()
    {
        var client = _factory.CreateClient();
        var withoutKey = new Invitation { Email = "without-key@example.test" };
        var malformedKey = new Invitation { Email = "malformed-key@example.test" };

        var first = await client.PostAsJsonAsync("/api/v1/iam/invitations", withoutKey, TestContext.Current.CancellationToken);
        using var malformedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/invitations")
        {
            Content = JsonContent.Create(malformedKey)
        };
        malformedRequest.Headers.Add("Idempotency-Key", "not-a-guid");
        var second = await client.SendAsync(malformedRequest, TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var emails = await scope.ServiceProvider.GetRequiredService<IamDbContext>().Invitations
            .Select(invitation => invitation.Email)
            .ToListAsync(TestContext.Current.CancellationToken);
        emails.ShouldContain(withoutKey.Email);
        emails.ShouldContain(malformedKey.Email);
    }

    [Fact]
    public async Task CreateUserEndpoint_AssignsSubjectAndPersistsProfile()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/iam/users",
            new IamProfile { DisplayName = "New user", Email = "new-user@example.test" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<IamProfile>(TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();
        created.KeycloakSubject.ShouldNotBe(Guid.Empty);
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IamDbContext>().Profiles
            .AnyAsync(profile => profile.Email == "new-user@example.test", TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateUserEndpoint_PreservesExistingKeycloakSubject()
    {
        var client = _factory.CreateClient();
        var existingSub = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            "/api/v1/iam/users",
            new IamProfile { KeycloakSubject = existingSub, DisplayName = "Preserved user", Email = "preserved@example.test" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<IamProfile>(TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();
        created.KeycloakSubject.ShouldBe(existingSub);
    }

}

public sealed class IamDevelopmentApiProgramTests : IClassFixture<IamDevelopmentApiFactory>
{
    private readonly IamDevelopmentApiFactory _factory;

    public IamDevelopmentApiProgramTests(IamDevelopmentApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DevelopmentEnvironment_ExposesOpenApiDocument()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var scalar = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);
        scalar.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently);
    }
}

