using IAM.Application;
using IAM.Infrastructure;
using IAM.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace IAM.UnitTests;

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
    public void AddInfrastructureServices_ShouldConfigureIamDbContextFromNamedConnection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:iam_db"] = "Host=localhost;Database=iam_test;Username=postgres;Password=postgres",
                ["EventBus:HostAddress"] = "amqp://guest:guest@localhost:5672"
            })
            .Build();

        services.AddInfrastructureServices(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();

        db.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
        db.Database.GetDbConnection().Database.ShouldBe("iam_test");
    }
}
