using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application;
using Notification.Infrastructure;
using Notification.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace Notification.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_ShouldRegisterMediatR()
    {
        var services = new ServiceCollection();

        services.AddApplicationServices();

        services.ShouldContain(x => x.ServiceType == typeof(IMediator));
    }

    [Fact]
    public void AddInfrastructureServices_ShouldConfigureNotificationDbContextFromNamedConnection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:notification_db"] = "Host=localhost;Database=notification_test;Username=postgres;Password=postgres",
                ["EventBus:HostAddress"] = "amqp://guest:guest@localhost:5672"
            })
            .Build();

        services.AddInfrastructureServices(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        db.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
        db.Database.GetDbConnection().Database.ShouldBe("notification_test");
    }
}
