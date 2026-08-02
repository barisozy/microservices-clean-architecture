using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Infrastructure.Data;
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

public sealed class NotificationApiProgramTests : IClassFixture<NotificationApiFactory>
{
    private readonly NotificationApiFactory _factory;
    public NotificationApiProgramTests(NotificationApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/v1/notification/logs")]
    [InlineData("/openapi/v1.json")]
    public async Task EventDrivenService_DoesNotExposeRestOrOpenApiSurface(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
