using System.Net.Http.Json;
using ECommerce.Contracts.Events.v1;
using ECommerce.Contracts.Protos;
using Catalog.Domain.Entities;
using Catalog.Infrastructure.Data;
using Fulfillment.Infrastructure;
using Inventory.Infrastructure.Data;
using Inventory.Domain.Entities;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Api.Endpoints;
using Order.Infrastructure.Data;
using Payment.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ECommerce.IntegrationTests;

/// <summary>
/// Sprint 1 and Sprint 2 acceptance tests. PostgreSQL, RabbitMQ, Valkey,
/// Keycloak, Inventory gRPC, Payment, and Fulfillment are all real components.
/// </summary>
[Collection("IntegrationTests")]
public sealed class OrderE2ETest : IAsyncLifetime
{
    private static readonly SemaphoreSlim TestLock = new(1, 1);
    private readonly InfrastructureFixture _infra;
    private bool _testLockHeld;
    private ServiceFactory<Inventory.Api.IInventoryApiMarker> _inventoryFactory = null!;
    private ServiceFactory<Payment.Api.IPaymentApiMarker> _paymentFactory = null!;
    private ServiceFactory<Fulfillment.Api.IFulfillmentApiMarker> _fulfillmentFactory = null!;
    private ServiceFactory<Catalog.Api.ICatalogApiMarker> _catalogFactory = null!;
    private ServiceFactory<Order.Api.IOrderApiMarker> _orderFactory = null!;
    private HttpClient _orderClient = null!;
    private string _accessToken = string.Empty;

    public OrderE2ETest(InfrastructureFixture infra) => _infra = infra;

    public async ValueTask InitializeAsync()
    {
        await TestLock.WaitAsync(CancellationToken.None);
        _testLockHeld = true;

        try
        {
            _accessToken = await _infra.GetCustomerAccessTokenAsync(TestContext.Current.CancellationToken);
            _inventoryFactory = new ServiceFactory<Inventory.Api.IInventoryApiMarker>(_infra);
            _paymentFactory = new ServiceFactory<Payment.Api.IPaymentApiMarker>(_infra);
            _fulfillmentFactory = new ServiceFactory<Fulfillment.Api.IFulfillmentApiMarker>(_infra);
            _catalogFactory = new ServiceFactory<Catalog.Api.ICatalogApiMarker>(_infra);

            _inventoryFactory.CreateClient().Dispose();
            _paymentFactory.CreateClient().Dispose();
            _fulfillmentFactory.CreateClient().Dispose();
            _catalogFactory.CreateClient().Dispose();

            await Task.WhenAll(
                StartBusAsync(_inventoryFactory),
                StartBusAsync(_paymentFactory),
                StartBusAsync(_fulfillmentFactory),
                StartBusAsync(_catalogFactory));

            await SeedCatalogAndInventoryAsync();

            _orderFactory = new ServiceFactory<Order.Api.IOrderApiMarker>(
                _infra,
                orderDependencies: new OrderServiceDependencies(_inventoryFactory, _catalogFactory, _accessToken));
            _orderClient = _orderFactory.CreateClient();
            _orderClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            await StartBusAsync(_orderFactory);
        }
        catch
        {
            _testLockHeld = false;
            TestLock.Release();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _orderClient.Dispose();
            await StopBusAsync(_orderFactory);
            await StopBusAsync(_fulfillmentFactory);
            await StopBusAsync(_catalogFactory);
            await StopBusAsync(_paymentFactory);
            await StopBusAsync(_inventoryFactory);
            await _orderFactory.DisposeAsync();
            await _fulfillmentFactory.DisposeAsync();
            await _catalogFactory.DisposeAsync();
            await _paymentFactory.DisposeAsync();
            await _inventoryFactory.DisposeAsync();
        }
        finally
        {
            if (_testLockHeld)
            {
                _testLockHeld = false;
                TestLock.Release();
            }
        }
    }

    private static async Task StopBusAsync<TProgram>(ServiceFactory<TProgram> factory)
        where TProgram : class
    {
        if (factory.Services.GetService<IBusControl>() is { } bus)
            await bus.StopAsync();
    }

    private static Task StartBusAsync<TProgram>(ServiceFactory<TProgram> factory)
        where TProgram : class =>
        factory.Services.GetRequiredService<IBusControl>().StartAsync();

    [Fact]
    public async Task InventoryGrpcService_AcceptsTheKeycloakJwt()
    {
        using var channel = GrpcChannel.ForAddress("http://inventory.integration.test", new GrpcChannelOptions
        {
            HttpHandler = new OrderServiceDependencies(_inventoryFactory, _catalogFactory, _accessToken).CreateInventoryHandler()
        });
        var client = new InventoryService.InventoryServiceClient(channel);
        var result = await client.ReserveStockAsync(new ReserveStockRequest
        {
            OrderId = Guid.CreateVersion7().ToString("D"),
            Sku = "GRPC-AUTH-CHECK",
            Quantity = 1
        }, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Message);
    }

    [Fact]
    public async Task CreateOrder_WithKeycloakJwt_PublishesOutboxMessage_AndShipsOrder()
    {
        var orderId = await CreateOrderAsync("PROD-SUCCESS");

        await EventuallyAsync(async () =>
        {
            using var scope = _paymentFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            return await db.Payment.AnyAsync(payment => payment.OrderId == orderId, TestContext.Current.CancellationToken);
        }, diagnostics: GetPaymentDeliveryDiagnosticsAsync);

        await EventuallyAsync(async () =>
        {
            using var scope = _orderFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId, TestContext.Current.CancellationToken);
            return order?.Status == OrderStatus.Shipped;
        }, TimeSpan.FromSeconds(60), () => GetOrderLifecycleDiagnosticsAsync(orderId));

        using var fulfillmentScope = _fulfillmentFactory.Services.CreateScope();
        var fulfillmentDb = fulfillmentScope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();
        var shipment = await fulfillmentDb.Shipments.SingleAsync(x => x.OrderId == orderId, TestContext.Current.CancellationToken);
        shipment.Status.ShouldBe("SHIPPED");
        shipment.TrackingNumber.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DuplicateIdempotencyKey_ReturnsTheOriginalOrderId()
    {
        var key = Guid.CreateVersion7().ToString("D");
        var first = await CreateOrderAsync("PROD-IDEMPOTENT", key);
        var second = await CreateOrderAsync("PROD-IDEMPOTENT", key);

        second.ShouldBe(first);
    }

    [Fact]
    public async Task PaymentFailure_CancelsOrder_ReleasesStock_AndDeduplicatesRedelivery()
    {
        var orderId = await CreateOrderAsync("FAIL_PAYMENT-SKU");

        await EventuallyAsync(async () =>
        {
            using var scope = _orderFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId, TestContext.Current.CancellationToken);
            return order?.Status == OrderStatus.Cancelled;
        }, TimeSpan.FromSeconds(5));

        await EventuallyAsync(async () =>
        {
            using var scope = _inventoryFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var reservation = await db.Reservations.SingleOrDefaultAsync(x => x.OrderId == orderId, TestContext.Current.CancellationToken);
            return reservation?.IsReleased == true;
        }, TimeSpan.FromSeconds(5));

        var duplicateMessageId = Guid.CreateVersion7();
        var duplicate = new PaymentFailed(orderId, Guid.CreateVersion7().ToString("D"), "duplicate delivery", DateTimeOffset.UtcNow);
        var bus = _orderFactory.Services.GetRequiredService<IBus>();
        await bus.Publish(duplicate, context => context.MessageId = duplicateMessageId, TestContext.Current.CancellationToken);
        await bus.Publish(duplicate, context => context.MessageId = duplicateMessageId, TestContext.Current.CancellationToken);

        await EventuallyAsync(async () =>
        {
            using var scope = _orderFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            return await db.Set<InboxState>()
                .Where(x => x.MessageId == duplicateMessageId)
                .Select(x => x.MessageId)
                .Distinct()
                .CountAsync(TestContext.Current.CancellationToken) == 1;
        });
    }

    [Fact]
    public async Task PaymentFailure_WithMultipleItems_ReleasesAllReservations_AndRestoresStock()
    {
        var skus = new[]
        {
            "FAIL_PAYMENT-SKU-1",
            "FAIL_PAYMENT-SKU-2",
            "FAIL_PAYMENT-SKU-3",
            "FAIL_PAYMENT-SKU-4"
        };

        var orderId = await CreateOrderAsync(skus);

        await EventuallyAsync(async () =>
        {
            using var scope = _orderFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = await db.Orders.SingleOrDefaultAsync(
                x => x.Id == orderId,
                TestContext.Current.CancellationToken);
            return order?.Status == OrderStatus.Cancelled;
        });

        await EventuallyAsync(async () =>
        {
            using var scope = _inventoryFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var reservations = await db.Reservations
                .Where(x => x.OrderId == orderId)
                .ToListAsync(TestContext.Current.CancellationToken);

            return reservations.Count == 1 &&
                   reservations.All(x => x.IsReleased);
        });

        await EventuallyAsync(async () =>
        {
            using var scope = _inventoryFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var stocks = await db.Stocks
                .Where(x => skus.Contains(x.Sku))
                .ToListAsync(TestContext.Current.CancellationToken);

            return stocks.Count == skus.Length &&
                   stocks.All(x => x.AvailableQuantity == 100);
        });

        var duplicate = new PaymentFailed(
            orderId,
            Guid.CreateVersion7().ToString("D"),
            "duplicate compensation",
            DateTimeOffset.UtcNow);
        var duplicateMessageId = Guid.CreateVersion7();
        var bus = _orderFactory.Services.GetRequiredService<IBus>();
        await bus.Publish(
            duplicate,
            context => context.MessageId = duplicateMessageId,
            TestContext.Current.CancellationToken);
        await bus.Publish(
            duplicate,
            context => context.MessageId = duplicateMessageId,
            TestContext.Current.CancellationToken);

        await EventuallyAsync(async () =>
        {
            using var scope = _orderFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            return await db.Set<InboxState>()
                .Where(x => x.MessageId == duplicateMessageId)
                .Select(x => x.MessageId)
                .Distinct()
                .CountAsync(TestContext.Current.CancellationToken) == 1;
        });

        using var finalScope = _inventoryFactory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var finalReservations = await finalDb.Reservations
            .Where(x => x.OrderId == orderId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var finalStocks = await finalDb.Stocks
            .Where(x => skus.Contains(x.Sku))
            .ToListAsync(TestContext.Current.CancellationToken);

        finalReservations.Count.ShouldBe(1);
        finalReservations.ShouldAllBe(x => x.IsReleased);
        finalStocks.Count.ShouldBe(skus.Length);
        finalStocks.ShouldAllBe(x => x.AvailableQuantity == 100);
    }

    private async Task<Guid> CreateOrderAsync(
        IReadOnlyCollection<string> skus,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.CreateVersion7().ToString("D"));
        request.Content = JsonContent.Create(new
        {
            items = skus.Select(sku => new { sku, quantity = 1, unitPrice = 100m }).ToArray()
        });

        using var response = await _orderClient.SendAsync(request, TestContext.Current.CancellationToken);
        var diagnostic = response.Headers.TryGetValues("X-Integration-Authentication-Failure", out var values)
            ? string.Join("; ", values)
            : await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Accepted, diagnostic);
        var submission = await response.Content.ReadFromJsonAsync<OrderSubmissionResponse>(TestContext.Current.CancellationToken);
        submission.ShouldNotBeNull();
        submission!.OrderId.ShouldNotBe(Guid.Empty);
        submission.Status.ShouldBe("PendingInventory");
        return submission.OrderId;
    }

    private async Task<Guid> CreateOrderAsync(string sku, string? idempotencyKey = null)
    {
        return await CreateOrderAsync(new[] { sku }, idempotencyKey);
    }

    private async Task SeedCatalogAndInventoryAsync()
    {
        var skus = new[]
        {
            "PROD-SUCCESS",
            "PROD-IDEMPOTENT",
            "FAIL_PAYMENT-SKU",
            "GRPC-AUTH-CHECK",
            "FAIL_PAYMENT-SKU-1",
            "FAIL_PAYMENT-SKU-2",
            "FAIL_PAYMENT-SKU-3",
            "FAIL_PAYMENT-SKU-4"
        };

        using (var scope = _catalogFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            foreach (var sku in skus)
            {
                if (!await db.Products.AnyAsync(product => product.Sku == sku, TestContext.Current.CancellationToken))
                {
                    db.Products.Add(new Product
                    {
                        Sku = sku,
                        Name = $"Integration {sku}",
                        Description = "Integration test product",
                        Price = 100m
                    });
                }
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var scope = _inventoryFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            foreach (var sku in skus)
            {
                if (!await db.Stocks.AnyAsync(stock => stock.Sku == sku, TestContext.Current.CancellationToken))
                    db.Stocks.Add(new Stock(sku, 100));
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<string> GetPaymentDeliveryDiagnosticsAsync()
    {
        using var scope = _orderFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var messages = await db.Set<OutboxMessage>()
            .Where(message => message.MessageType.Contains(nameof(OrderCheckoutCompleted)))
            .Select(message => new
            {
                message.SequenceNumber,
                message.OutboxId,
                message.MessageType,
                message.DestinationAddress,
                message.SourceAddress,
                message.SentTime,
                message.EnqueueTime
            })
            .ToListAsync(TestContext.Current.CancellationToken);
        var states = await db.Set<OutboxState>()
            .Select(state => new { state.OutboxId, state.Created, state.Delivered, state.LastSequenceNumber })
            .ToListAsync(TestContext.Current.CancellationToken);
        var hostedServices = _orderFactory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .Select(service => service.GetType().FullName)
            .ToArray();
        var rabbit = await _infra.GetRabbitMqDiagnosticsAsync(TestContext.Current.CancellationToken);
        return $"Outbox messages: {System.Text.Json.JsonSerializer.Serialize(messages)}; " +
               $"outbox states: {System.Text.Json.JsonSerializer.Serialize(states)}; " +
               $"hosted services: {System.Text.Json.JsonSerializer.Serialize(hostedServices)}; RabbitMQ: {rabbit}";
    }

    private async Task<string> GetOrderLifecycleDiagnosticsAsync(Guid orderId)
    {
        using var scope = _orderFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders
            .Where(candidate => candidate.Id == orderId)
            .Select(candidate => new { candidate.Id, candidate.Status })
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        var rabbit = await _infra.GetRabbitMqDiagnosticsAsync(TestContext.Current.CancellationToken);
        return $"Order: {System.Text.Json.JsonSerializer.Serialize(order)}; RabbitMQ: {rabbit}";
    }

    private static async Task EventuallyAsync(
        Func<Task<bool>> assertion,
        TimeSpan? timeout = null,
        Func<Task<string>>? diagnostics = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < deadline)
        {
            if (await assertion()) return;
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        var diagnosticMessage = diagnostics is null
            ? string.Empty
            : $" RabbitMQ diagnostics: {await diagnostics()}";
        (await assertion()).ShouldBeTrue($"The expected asynchronous workflow did not complete before the timeout.{diagnosticMessage}");
    }
}
