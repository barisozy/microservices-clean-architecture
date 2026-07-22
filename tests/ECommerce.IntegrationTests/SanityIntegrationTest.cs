using Shouldly;
using Xunit;

namespace ECommerce.IntegrationTests;

/// <summary>
/// Sprint 1 Definition of Done integration test.
///
/// This test verifies that the Testcontainers infrastructure fixtures start correctly.
/// More comprehensive subcutaneous tests (direct Application layer invocation with
/// real Postgres + RabbitMQ containers) can be added per sprint as the system grows.
///
/// The definitive E2E flow test (POST /api/v1/orders → connected trace in Aspire Dashboard)
/// is performed manually against the running docker-compose stack per the Sprint DoD.
/// </summary>
[Collection("IntegrationTests")]
public class InfrastructureAvailabilityTest(InfrastructureFixture fixture)
{
    [Fact]
    public void PostgresContainer_ShouldBeRunning()
    {
        fixture.PostgresContainer.State
            .ShouldBe(DotNet.Testcontainers.Containers.TestcontainersStates.Running);
    }

    [Fact]
    public void RabbitMqContainer_ShouldBeRunning()
    {
        fixture.RabbitMqContainer.State
            .ShouldBe(DotNet.Testcontainers.Containers.TestcontainersStates.Running);
    }

    [Fact]
    public void PostgresContainer_ShouldHaveConnectionString()
    {
        var connStr = fixture.PostgresContainer.GetConnectionString();
        connStr.ShouldNotBeNullOrEmpty();
        connStr.ShouldContain("Host=");
    }

    [Fact]
    public void RabbitMqContainer_ShouldHaveConnectionString()
    {
        var connStr = fixture.RabbitMqContainer.GetConnectionString();
        connStr.ShouldNotBeNullOrEmpty();
        connStr.ShouldContain("amqp://");
    }
}

/// <summary>
/// Sprint 1 idempotency contract test — pure logic, no infrastructure needed.
/// Documents the IdempotencyKey uniqueness requirement.
/// </summary>
public class IdempotencyKeyContractTest
{
    [Fact]
    public void GuidCreateVersion7_ShouldProduceUniqueKeys()
    {
        var key1 = Guid.CreateVersion7().ToString();
        var key2 = Guid.CreateVersion7().ToString();

        key1.ShouldNotBe(key2);
        key1.ShouldNotBeNullOrEmpty();
        Guid.TryParse(key1, out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("12345678-abcd-efgh-ijkl-mnop12345678")]
    public void InvalidIdempotencyKey_ShouldFailGuidParse(string invalidKey)
    {
        Guid.TryParse(invalidKey, out _).ShouldBeFalse();
    }
}

