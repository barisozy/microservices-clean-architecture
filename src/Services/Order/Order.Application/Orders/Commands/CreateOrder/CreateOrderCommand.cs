using System.Diagnostics.Metrics;
using ECommerce.Contracts.Events.v1;
using ECommerce.Contracts.Protos;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Order.Application.Common.Interfaces;
using Order.Application.Common.Exceptions;
using Order.Domain.Entities;
using Order.Domain.Exceptions;

namespace Order.Application.Orders.Commands.CreateOrder;

public record OrderItemDto(string Sku, int Quantity, decimal UnitPrice);

public record CreateOrderCommand(
    Guid CustomerId,
    Guid KeycloakSubject,
    string IdempotencyKey,
    List<OrderItemDto>? Items = null,
    string? CouponCode = null) : IRequest<Guid>;

public class CreateOrderCommandHandler(
    IOrderDbContext context,
    IPublishEndpoint publishEndpoint,
    IOrderCache orderCache,
    IBasketService basketService,
    InventoryService.InventoryServiceClient inventoryClient,
    CatalogService.CatalogServiceClient catalogClient,
    PromotionService.PromotionServiceClient promotionClient,
    ILogger<CreateOrderCommandHandler> logger) : IRequestHandler<CreateOrderCommand, Guid>
{
    private static readonly Meter Meter = new("Order.Api");
    private static readonly Histogram<double> CheckoutDuration =
        Meter.CreateHistogram<double>("order.checkout.duration", "ms");
    private static readonly Histogram<double> CatalogDuration =
        Meter.CreateHistogram<double>("catalog.price_snapshot.duration", "ms");
    private static readonly Histogram<double> InventoryDuration =
        Meter.CreateHistogram<double>("inventory.reserve_stock.duration", "ms");
    private static readonly Histogram<double> PromotionDuration =
        Meter.CreateHistogram<double>("promotion.coupon_apply.duration", "ms");

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        using var checkoutTiming = new DurationScope(CheckoutDuration);
        try
        {
            var cachedOrderId = await orderCache.GetOrderIdAsync(request.IdempotencyKey, cancellationToken);
            if (cachedOrderId.HasValue)
            {
                return cachedOrderId.Value;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Valkey idempotency lookup failed; falling back to PostgreSQL");
        }

        var persistedOrder = await context.FindByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (persistedOrder is not null)
        {
            return persistedOrder.Id;
        }

        var orderItemsDto = request.Items ?? new List<OrderItemDto>();

        // Sprint 1 / Task 20: Checkout-from-basket mode (empty Items[])
        bool isCheckoutFromBasket = orderItemsDto.Count == 0;
        IAsyncDisposable? basketLock = null;
        if (isCheckoutFromBasket)
        {
            try
            {
                basketLock = await orderCache.TryAcquireBasketLockAsync(
                    request.KeycloakSubject.ToString("D"),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                throw new BasketUnavailableException(
                    "Basket checkout is temporarily unavailable because the basket lock could not be acquired.",
                    exception);
            }

            if (basketLock is null)
            {
                throw new BasketUnavailableException("Checkout is already in progress for this basket.");
            }
        }

        await using var heldBasketLock = basketLock;

        if (isCheckoutFromBasket)
        {
            var basket = await basketService.GetBasketAsync(request.KeycloakSubject.ToString("D"), cancellationToken);
            if (basket.Count == 0)
            {
                throw new OrderDomainException("Basket is empty. Cannot checkout.");
            }

            orderItemsDto = basket.Select(kvp => new OrderItemDto(kvp.Key, kvp.Value, 0m)).ToList();
        }

        // Sprint 5: Catalog.Api GetPriceSnapshot gRPC — snapshot unit price to protect historical order data
        var finalItems = new List<OrderItem>();
        foreach (var item in orderItemsDto)
        {
            decimal unitPrice = item.UnitPrice;
            try
            {
                GetPriceSnapshotResponse priceSnapshot;
                using (new DurationScope(CatalogDuration))
                {
                    priceSnapshot = await catalogClient.GetPriceSnapshotAsync(
                        new GetPriceSnapshotRequest { Sku = item.Sku }, cancellationToken: cancellationToken);
                }
                if (priceSnapshot.Available && priceSnapshot.UnitPrice > 0)
                {
                    unitPrice = (decimal)priceSnapshot.UnitPrice;
                    try
                    {
                        await orderCache.SetCatalogPriceAsync(item.Sku, unitPrice, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        logger.LogWarning(exception, "Could not cache catalog price for SKU {Sku}", item.Sku);
                    }
                }
            }
            catch (Exception ex)
            {
                decimal? cachedPrice = null;
                try
                {
                    cachedPrice = await orderCache.GetCatalogPriceAsync(item.Sku, cancellationToken);
                }
                catch (Exception cacheException)
                {
                    logger.LogWarning(cacheException, "Catalog price cache lookup failed for SKU {Sku}", item.Sku);
                }

                if (cachedPrice.HasValue)
                {
                    unitPrice = cachedPrice.Value;
                }

                logger.LogWarning(
                    ex,
                    "Catalog price snapshot failed for SKU {Sku}; cached snapshot availability: {HasCachedPrice}",
                    item.Sku,
                    cachedPrice.HasValue);
            }

            finalItems.Add(new OrderItem
            {
                Sku = item.Sku,
                Quantity = item.Quantity,
                UnitPrice = unitPrice
            });
        }

        // Sprint 1: Sync gRPC call to Inventory.Api — reserve stock BEFORE creating the order
        var order = global::Order.Domain.Entities.Order.Create(request.CustomerId.ToString(), request.IdempotencyKey, finalItems);

        var reservationIds = new List<string>();
        foreach (var item in finalItems)
        {
            ReserveStockResponse reserveResponse;
            using (new DurationScope(InventoryDuration))
            {
                reserveResponse = await inventoryClient.ReserveStockAsync(
                    new ReserveStockRequest
                    {
                        OrderId = order.Id.ToString("D"),
                        Sku = item.Sku,
                        Quantity = item.Quantity
                    },
                    cancellationToken: cancellationToken);
            }

            if (!reserveResponse.IsSuccess)
            {
                foreach (var reservationId in reservationIds)
                {
                    await inventoryClient.ReleaseStockAsync(
                        new ReleaseStockRequest { ReservationId = reservationId },
                        cancellationToken: cancellationToken);
                }

                throw new OrderDomainException($"Stock reservation failed: {reserveResponse.Message}");
            }

            if (!string.IsNullOrWhiteSpace(reserveResponse.ReservationId))
            {
                reservationIds.Add(reserveResponse.ReservationId);
            }
        }

        // Sprint 7: Promotion.Api ApplyCoupon gRPC call
        var totalAmount = finalItems.Sum(i => i.Quantity * i.UnitPrice);
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            try
            {
                ApplyCouponResponse couponResult;
                using (new DurationScope(PromotionDuration))
                {
                    couponResult = await promotionClient.ApplyCouponAsync(
                        new ApplyCouponRequest
                        {
                            Code = request.CouponCode,
                            OrderTotal = (double)totalAmount
                        }, cancellationToken: cancellationToken);
                }

                if (couponResult.IsValid && couponResult.DiscountedTotal < (double)totalAmount)
                {
                    logger.LogInformation("Applied coupon {Code}. Original: {Total}, Discounted: {Discounted}",
                        request.CouponCode, totalAmount, couponResult.DiscountedTotal);
                    totalAmount = (decimal)couponResult.DiscountedTotal;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to apply coupon {Code}. Proceeding with original total.", request.CouponCode);
            }
        }

        context.Orders.Add(order);

        var eventItems = finalItems.Select(i => new OrderItemContractDto(i.Sku, i.Quantity, i.UnitPrice)).ToList();

        // Publish via MassTransit Outbox — after stock is reserved
        await publishEndpoint.Publish(new OrderCreated(order.Id, request.CustomerId, request.IdempotencyKey, eventItems, totalAmount, DateTimeOffset.UtcNow), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        // Clear basket if checked out from basket
        if (isCheckoutFromBasket)
        {
            await basketService.DeleteBasketAsync(request.KeycloakSubject.ToString(), cancellationToken);
        }

        try
        {
            await orderCache.SetOrderIdAsync(request.IdempotencyKey, order.Id, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Order {OrderId} persisted but Valkey idempotency cache update failed", order.Id);
        }

        return order.Id;
    }

    private sealed class DurationScope(Histogram<double> histogram) : IDisposable
    {
        private readonly long _startedAt = TimeProvider.System.GetTimestamp();

        public void Dispose() =>
            histogram.Record(TimeProvider.System.GetElapsedTime(_startedAt).TotalMilliseconds);
    }
}
