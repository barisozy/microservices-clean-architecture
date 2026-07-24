using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Auditing;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Shouldly;
using Xunit;

namespace Auditing.UnitTests;

public class SampleProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AuditLogDummy
{
    public int Id { get; set; }
    public string Data { get; set; } = string.Empty;
}

public class OutboxItem
{
    public int Id { get; set; }
}

public class InboxItem
{
    public int Id { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public Address Address { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = string.Empty;
}

public class TestInterceptorDbContext : DbContext
{
    public TestInterceptorDbContext(DbContextOptions<TestInterceptorDbContext> options) : base(options) { }

    public DbSet<SampleProduct> Products => Set<SampleProduct>();
    public DbSet<AuditLogDummy> AuditLogDummies => Set<AuditLogDummy>();
    public DbSet<OutboxItem> OutboxItems => Set<OutboxItem>();
    public DbSet<InboxItem> InboxItems => Set<InboxItem>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Customer>().OwnsOne(c => c.Address);
    }
}

public class AuditInterceptorTests
{
    private DbContextOptions<TestInterceptorDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<TestInterceptorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task SavingChangesAsync_ShouldReturnBaseResult_WhenDbContextIsNull()
    {
        // Arrange
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var interceptor = new AuditInterceptor(publishEndpointMock.Object, httpContextAccessorMock.Object);

        var eventData = new DbContextEventData(
            null!,
            (flags, eventData) => "test",
            context: null
        );

        // Act
        var result = await interceptor.SavingChangesAsync(eventData, default, TestContext.Current.CancellationToken);

        // Assert
        publishEndpointMock.Verify(x => x.Publish(It.IsAny<AuditLogCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SavingChangesAsync_ShouldPublishAuditLog_ForAddedEntity()
    {
        // Arrange
        var options = CreateNewContextOptions();
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[] 
        { 
            new Claim(ClaimTypes.NameIdentifier, "user-456"),
            new Claim(ClaimTypes.Role, "User")
        }, "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
        context.TraceIdentifier = "trace-999";

        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        var interceptor = new AuditInterceptor(publishEndpointMock.Object, httpContextAccessorMock.Object);

        using var dbContext = new TestInterceptorDbContext(options);
        dbContext.Products.Add(new SampleProduct { Id = 10, Name = "Laptop" });

        var eventData = new DbContextEventData(
            null!,
            (flags, eventData) => "test",
            context: dbContext
        );

        // Act
        await interceptor.SavingChangesAsync(eventData, default, TestContext.Current.CancellationToken);

        // Assert
        publishEndpointMock.Verify(x => x.Publish(It.Is<AuditLogCreated>(msg =>
            msg.UserId == "user-456" &&
            msg.UserRoles == "User" &&
            msg.IpAddress == "192.168.1.1" &&
            msg.UserAgent == "Mozilla/5.0" &&
            msg.Action == "Added" &&
            msg.EntityName == "SampleProduct" &&
            msg.EntityId == "10" &&
            msg.TraceId == "trace-999" &&
            msg.Changes.Contains("Laptop")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SavingChangesAsync_ShouldPublishAuditLog_ForModifiedEntity()
    {
        // Arrange
        var options = CreateNewContextOptions();
        using (var setupDb = new TestInterceptorDbContext(options))
        {
            setupDb.Products.Add(new SampleProduct { Id = 5, Name = "Phone" });
            await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var interceptor = new AuditInterceptor(publishEndpointMock.Object, httpContextAccessorMock.Object);

        using var dbContext = new TestInterceptorDbContext(options);
        var product = await dbContext.Products.FirstAsync(x => x.Id == 5, TestContext.Current.CancellationToken);
        product.Name = "Smart Phone";

        var eventData = new DbContextEventData(
            null!,
            (flags, eventData) => "test",
            context: dbContext
        );

        // Act
        await interceptor.SavingChangesAsync(eventData, default, TestContext.Current.CancellationToken);

        // Assert
        publishEndpointMock.Verify(x => x.Publish(It.Is<AuditLogCreated>(msg =>
            msg.UserId == "System" &&
            msg.Action == "Modified" &&
            msg.EntityName == "SampleProduct" &&
            msg.EntityId == "5" &&
            msg.Changes.Contains("Smart Phone")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SavingChangesAsync_ShouldPublishAuditLog_ForDeletedEntity()
    {
        // Arrange
        var options = CreateNewContextOptions();
        using (var setupDb = new TestInterceptorDbContext(options))
        {
            setupDb.Products.Add(new SampleProduct { Id = 7, Name = "Tablet" });
            await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var interceptor = new AuditInterceptor(publishEndpointMock.Object, httpContextAccessorMock.Object);

        using var dbContext = new TestInterceptorDbContext(options);
        var product = await dbContext.Products.FirstAsync(x => x.Id == 7, TestContext.Current.CancellationToken);
        dbContext.Products.Remove(product);

        var eventData = new DbContextEventData(
            null!,
            (flags, eventData) => "test",
            context: dbContext
        );

        // Act
        await interceptor.SavingChangesAsync(eventData, default, TestContext.Current.CancellationToken);

        // Assert
        publishEndpointMock.Verify(x => x.Publish(It.Is<AuditLogCreated>(msg =>
            msg.Action == "Deleted" &&
            msg.EntityName == "SampleProduct" &&
            msg.EntityId == "7"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SavingChangesAsync_ShouldSkip_AuditLog_Outbox_And_Inbox_Entities()
    {
        // Arrange
        var options = CreateNewContextOptions();
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var interceptor = new AuditInterceptor(publishEndpointMock.Object, httpContextAccessorMock.Object);

        using var dbContext = new TestInterceptorDbContext(options);
        dbContext.AuditLogDummies.Add(new AuditLogDummy { Id = 1, Data = "Log" });
        dbContext.OutboxItems.Add(new OutboxItem { Id = 1 });
        dbContext.InboxItems.Add(new InboxItem { Id = 1 });

        var eventData = new DbContextEventData(
            null!,
            (flags, eventData) => "test",
            context: dbContext
        );

        // Act
        await interceptor.SavingChangesAsync(eventData, default, TestContext.Current.CancellationToken);

        // Assert
        publishEndpointMock.Verify(x => x.Publish(It.IsAny<AuditLogCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SavingChangesAsync_ShouldFallbackToUnknownEntityId_WhenNoPrimaryKeyPropertyExists()
    {
        // Arrange
        var options = CreateNewContextOptions();
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var interceptor = new AuditInterceptor(publishEndpointMock.Object, httpContextAccessorMock.Object);

        using var dbContext = new TestInterceptorDbContext(options);
        var customer = new Customer { Id = 1, Address = new Address { Street = "Main St" } };
        dbContext.Customers.Add(customer);

        var eventData = new DbContextEventData(
            null!,
            (flags, eventData) => "test",
            context: dbContext
        );

        // Act
        await interceptor.SavingChangesAsync(eventData, default, TestContext.Current.CancellationToken);

        // Assert - Customer has primary key "1", Address owned entity has primary key property "CustomerId" or shadow key
        publishEndpointMock.Verify(x => x.Publish(It.Is<AuditLogCreated>(msg =>
            msg.EntityName == "Customer" &&
            msg.EntityId == "1"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }
}
