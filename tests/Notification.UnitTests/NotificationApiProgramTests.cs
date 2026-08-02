using System.Net;
using System.Net.Http.Json;
using Notification.Domain.Entities;
using Notification.Infrastructure.Data;
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

namespace Notification.UnitTests;

public class NotificationApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<NotificationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<NotificationDbContext>>();
            services.AddDbContext<NotificationDbContext>(options => options.UseInMemoryDatabase("notification-api-tests"));
            services.RemoveAll<IHostedService>();
        });
    }
}

public sealed class DevelopmentNotificationApiFactory : NotificationApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Development");
    }
}

public sealed class NotificationApiProgramTests : IClassFixture<NotificationApiFactory>
{
    private readonly NotificationApiFactory _factory;
    public NotificationApiProgramTests(NotificationApiFactory factory) => _factory = factory;

    [Fact]
    public async Task LogsEndpoint_ReturnsMostRecentLogs()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            db.Logs.RemoveRange(db.Logs);
            db.Logs.Add(new NotificationLog { Id = Guid.NewGuid(), RecipientEmail = "user@example.test", Subject = "Order shipped", Content = "Sent", SentAt = DateTime.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _factory.CreateClient().GetAsync("/api/v1/notification/logs", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("Order shipped");
    }

    [Fact]
    public async Task LogsEndpoint_ReturnsAtMostFiftyLogs_InDescendingSentAtOrder()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            db.Logs.RemoveRange(db.Logs);
            var start = DateTime.UtcNow.AddMinutes(-60);
            db.Logs.AddRange(Enumerable.Range(0, 55).Select(index => new NotificationLog
            {
                Id = Guid.NewGuid(),
                EventType = "OrderShipped",
                RecipientEmail = $"customer-{index}@example.test",
                Subject = $"Notification {index}",
                Content = "Sent",
                SentAt = start.AddMinutes(index)
            }));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _factory.CreateClient().GetAsync("/api/v1/notification/logs", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var logs = await response.Content.ReadFromJsonAsync<List<NotificationLog>>(TestContext.Current.CancellationToken);

        logs.ShouldNotBeNull();
        logs.Count.ShouldBe(50);
        logs[0].Subject.ShouldBe("Notification 54");
        logs[^1].Subject.ShouldBe("Notification 5");
    }
}

public sealed class DevelopmentNotificationApiProgramTests : IClassFixture<DevelopmentNotificationApiFactory>
{
    private readonly DevelopmentNotificationApiFactory _factory;

    public DevelopmentNotificationApiProgramTests(DevelopmentNotificationApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DevelopmentEnvironment_ExposesOpenApiDocument()
    {
        var response = await _factory.CreateClient().GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("notification/logs");
        var scalar = await _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }).GetAsync("/scalar", TestContext.Current.CancellationToken);
        scalar.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently);
    }
}

