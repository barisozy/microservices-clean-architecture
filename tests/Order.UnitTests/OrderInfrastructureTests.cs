using System.Security.Claims;
using System.Reflection;
using ECommerce.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Moq;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Queries;
using Order.Domain.Entities;
using Order.Infrastructure.Data;
using Order.Infrastructure.Data.Repositories;
using Order.Infrastructure.Data.Interceptors;
using Order.Infrastructure.Services;
using Order.Infrastructure;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Order.UnitTests;

public class OrderInfrastructureTests
{
    [Fact]
    public void AddInfrastructureServices_ShouldRegisterOrderDependenciesWithoutConnectingToExternalServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderDb"] = "Host=localhost;Database=order_test",
                ["ConnectionStrings:valkey"] = "localhost:6379,abortConnect=false",
                ["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672",
                ["services:inventory-api:http:0"] = "http://localhost:5001",
                ["services:catalog-api:http:0"] = "http://localhost:5002",
                ["services:promotion-api:http:0"] = "http://localhost:5003"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IOrderWriteRepository));
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IBasketService));
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IOrderReadRepository));
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer));
    }

    [Fact]
    public void OrderDbContext_ShouldConfigureOrderSchemaAndUniqueIdempotencyKey()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=order_test")
            .Options;
        using var context = new OrderDbContext(options);

        var orderType = context.Model.FindEntityType(typeof(global::Order.Domain.Entities.Order))!;
        orderType.GetSchema().ShouldBe("order");
        orderType.GetTableName().ShouldBe("Orders");
        orderType.GetIndexes().Single(index => index.Properties.Single().Name == nameof(global::Order.Domain.Entities.Order.IdempotencyKey)).IsUnique.ShouldBeTrue();

        var itemType = context.Model.FindEntityType(typeof(OrderItem))!;
        itemType.GetTableName().ShouldBe("OrderItems");
        itemType.FindProperty(nameof(OrderItem.Sku))!.GetMaxLength().ShouldBe(100);
    }

    [Fact]
    public void CurrentUser_ShouldReadNameIdentifier_AndReturnNullWithoutHttpContext()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "buyer-42")]))
            }
        };
        new CurrentUser(accessor).Id.ShouldBe("buyer-42");

        accessor.HttpContext = null;
        new CurrentUser(accessor).Id.ShouldBeNull();
    }

    [Fact]
    public async Task AuditableEntityInterceptor_ShouldStampAddedAndModifiedOrders()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "buyer-42")], "Test"))
            }
        };
        var interceptor = new ECommerce.Auditing.AuditableEntityInterceptor(accessor);
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        using var context = new OrderDbContext(options);
        var created = global::Order.Domain.Entities.Order.Create("buyer", "key", []);
        context.Orders.Add(created);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        created.CreatedAt.ShouldNotBe(default);
        created.LastModifiedAt.ShouldNotBe(default);
        created.CreatedBy.ShouldBe("buyer-42");

        var beforeUpdate = created.LastModifiedAt;
        created.Cancel("test update");
        context.Entry(created).State = EntityState.Modified;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        created.LastModifiedAt.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
    }

    [Fact]
    public async Task DispatchDomainEventsInterceptor_ShouldPublishAndClearEvents()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql("Host=localhost;Database=order_test").Options;
        using var context = new OrderDbContext(options);
        var order = global::Order.Domain.Entities.Order.Create("buyer", "key", []);
        context.Orders.Add(order);
        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher.Setup(x => x.Dispatch(It.IsAny<Order.Domain.Common.BaseEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var interceptor = new DispatchDomainEventsInterceptor(dispatcher.Object);

        var method = typeof(DispatchDomainEventsInterceptor).GetMethod("DispatchDomainEvents", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(interceptor, [context, CancellationToken.None])!;

        dispatcher.Verify(
            value => value.Dispatch(It.IsAny<Order.Domain.Common.BaseEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        order.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValkeyBasketService_ShouldReplaceBasketRefreshTtlAndDelete()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        database.Setup(x => x.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>())).Returns(Task.CompletedTask);
        database.Setup(x => x.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        database.Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([new HashEntry("SKU-1", 2)]);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        var service = new ValkeyBasketService(multiplexer.Object);

        (await service.GetBasketAsync("buyer", TestContext.Current.CancellationToken)).ShouldBe(new Dictionary<string, int> { ["SKU-1"] = 2 });
        (await service.SetBasketAsync("buyer", new Dictionary<string, int> { ["SKU-2"] = 3 }, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await service.DeleteBasketAsync("buyer", TestContext.Current.CancellationToken)).ShouldBeTrue();

        database.Verify(x => x.HashSetAsync("basket:buyer", It.Is<HashEntry[]>(entries => entries.Single().Name == "SKU-2" && entries.Single().Value == 3), It.IsAny<CommandFlags>()), Times.Once);
        database.Verify(x => x.KeyExpireAsync("basket:buyer", TimeSpan.FromDays(7), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ValkeyBasketService_ShouldKeepEmptyBasketDeletedWhileRefreshingTtl()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        database.Setup(x => x.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);

        (await new ValkeyBasketService(multiplexer.Object).SetBasketAsync("buyer", [], TestContext.Current.CancellationToken)).ShouldBeTrue();
        database.Verify(x => x.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()), Times.Never);
        database.Verify(x => x.KeyExpireAsync("basket:buyer", TimeSpan.FromDays(7), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task OrderReadRepository_ShouldDeserializeStoredModelAndHandleMissingValue()
    {
        var database = new Mock<IDatabase>();
        var id = Guid.NewGuid();
        database.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("{\"Id\":\"" + id + "\",\"Status\":\"Pending\",\"BuyerId\":\"buyer\"}");
        database.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        var repository = new OrderReadRepository(multiplexer.Object);

        var result = await repository.GetOrderAsync(id, TestContext.Current.CancellationToken);
        result.ShouldBe(new OrderStatusDto(id, "Pending", "buyer"));
        await repository.SetOrderAsync(new OrderStatusDto(id, "Paid", "buyer"), TestContext.Current.CancellationToken);
        database.Invocations.ShouldContain(invocation => invocation.Method.Name == nameof(IDatabase.StringSetAsync)
            && invocation.Arguments[0].ToString() == "order-read-model:" + id
            && invocation.Arguments[1].ToString()!.Contains("Paid"));

        database.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);
        (await repository.GetOrderAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).ShouldBeNull();
    }

}

