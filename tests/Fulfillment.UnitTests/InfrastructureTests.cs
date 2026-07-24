using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Fulfillment.Domain.Entities;
using Fulfillment.Infrastructure;
using Fulfillment.Infrastructure.Data.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Fulfillment.UnitTests;

public class InfrastructureTests
{
    [Fact]
    public async Task FulfillmentReadRepository_GetAndSet_ShouldInteractWithValkeyDatabase()
    {
        // Arrange
        var valkeyMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        valkeyMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var orderId = Guid.NewGuid();
        dbMock.Setup(x => x.StringGetAsync($"fulfillment-read-model:{orderId}", CommandFlags.None))
              .ReturnsAsync("Delivered");

        var repo = new FulfillmentReadRepository(valkeyMock.Object);

        // Act
        var status = await repo.GetFulfillmentStatusAsync(orderId, TestContext.Current.CancellationToken);
        await repo.SetFulfillmentStatusAsync(orderId, "Delivered", TestContext.Current.CancellationToken);

        // Assert
        status.ShouldBe("Delivered");
        dbMock.Verify(x => x.StringSetAsync($"fulfillment-read-model:{orderId}", "Delivered", null, false, When.Always, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task FulfillmentReadRepository_Get_ShouldReturnNull_WhenRedisReturnsNull()
    {
        // Arrange
        var valkeyMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        valkeyMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var orderId = Guid.NewGuid();
        dbMock.Setup(x => x.StringGetAsync($"fulfillment-read-model:{orderId}", CommandFlags.None))
              .ReturnsAsync(RedisValue.Null);

        var repo = new FulfillmentReadRepository(valkeyMock.Object);

        // Act
        var status = await repo.GetFulfillmentStatusAsync(orderId, TestContext.Current.CancellationToken);

        // Assert
        status.ShouldBeNull();
    }

    [Fact]
    public void CurrentUser_ShouldExtractUserIdFromHttpContext()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-abc-123") }));
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        var currentUser = new CurrentUser(httpContextAccessorMock.Object);

        // Act & Assert
        currentUser.Id.ShouldBe("user-abc-123");
    }

    [Fact]
    public void CurrentUser_ShouldReturnNull_WhenHttpContextIsNull()
    {
        // Arrange
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var currentUser = new CurrentUser(httpContextAccessorMock.Object);

        // Act & Assert
        currentUser.Id.ShouldBeNull();
    }

    [Fact]
    public async Task FulfillmentDbContext_EnsureCreated_ShouldConfigureModel()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<FulfillmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new FulfillmentDbContext(options);

        // Act
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Assert
        var entityType = dbContext.Model.FindEntityType(typeof(FulfillmentTask));
        entityType.ShouldNotBeNull();
    }
}
