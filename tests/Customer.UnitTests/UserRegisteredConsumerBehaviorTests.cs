using Customer.Domain.Entities;
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

public class UserRegisteredConsumerBehaviorTests
{
    [Fact]
    public async Task Consume_ShouldPersistEmailAndDeriveDisplayNameFromLocalPart()
    {
        await using var db = CreateDb();
        var subject = Guid.CreateVersion7();
        var consumer = new UserRegisteredConsumer(db, NullLogger<UserRegisteredConsumer>.Instance);

        await consumer.Consume(CreateContext(new UserRegistered(subject, "grace.hopper@example.test")));

        var profile = await db.Profiles.SingleAsync(TestContext.Current.CancellationToken);
        profile.KeycloakSubject.ShouldBe(subject);
        profile.Email.ShouldBe("grace.hopper@example.test");
        profile.DisplayName.ShouldBe("grace.hopper");
    }

    [Fact]
    public async Task Consume_ShouldNotOverwriteAnExistingProfile()
    {
        await using var db = CreateDb();
        var subject = Guid.CreateVersion7();
        db.Profiles.Add(new CustomerProfile
        {
            KeycloakSubject = subject, DisplayName = "Original", Email = "original@example.test"
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var consumer = new UserRegisteredConsumer(db, NullLogger<UserRegisteredConsumer>.Instance);

        await consumer.Consume(CreateContext(new UserRegistered(subject, "new@example.test")));

        var profile = await db.Profiles.SingleAsync(TestContext.Current.CancellationToken);
        profile.DisplayName.ShouldBe("Original");
        profile.Email.ShouldBe("original@example.test");
    }

    private static CustomerDbContext CreateDb() => new(new DbContextOptionsBuilder<CustomerDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ConsumeContext<UserRegistered> CreateContext(UserRegistered message)
    {
        var context = new Mock<ConsumeContext<UserRegistered>>();
        context.SetupGet(x => x.Message).Returns(message);
        return context.Object;
    }
}
