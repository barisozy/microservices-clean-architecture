using Customer.Infrastructure.Consumers;
using Customer.Infrastructure.Data;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace Customer.UnitTests;

public class UserRegisteredConsumerTests
{
    [Fact]
    public async Task Consume_ShouldCreateProfileOnlyOnce()
    {
        await using var db = new CustomerDbContext(new DbContextOptionsBuilder<CustomerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var message = new UserRegistered(Guid.CreateVersion7(), "ada@example.test");
        var context = new Mock<ConsumeContext<UserRegistered>>();
        context.SetupGet(x => x.Message).Returns(message);
        var consumer = new UserRegisteredConsumer(db, NullLogger<UserRegisteredConsumer>.Instance);

        await consumer.Consume(context.Object);
        await consumer.Consume(context.Object);

        db.Profiles.Count().ShouldBe(1);
        db.Profiles.Single().DisplayName.ShouldBe("ada");
    }
}
