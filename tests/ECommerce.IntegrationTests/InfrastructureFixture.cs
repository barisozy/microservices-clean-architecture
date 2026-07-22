using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace ECommerce.IntegrationTests;

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<InfrastructureFixture>
{
}

public class InfrastructureFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgresContainer { get; } = new PostgreSqlBuilder("postgres:18.4-alpine")
        .WithDatabase("ecommerce")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public RabbitMqContainer RabbitMqContainer { get; } = new RabbitMqBuilder("rabbitmq:4.3.1-management-alpine")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await PostgresContainer.StartAsync();
        await RabbitMqContainer.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await PostgresContainer.DisposeAsync();
        await RabbitMqContainer.DisposeAsync();
    }
}
