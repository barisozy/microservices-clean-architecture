using ECommerce.Contracts.Protos;
using Grpc.Core;
using Inventory.Application.Inventory.Commands;
using MediatR;

namespace Inventory.Api.Services;

public class InventoryGrpcService(
    ILogger<InventoryGrpcService> logger,
    ISender sender) : InventoryService.InventoryServiceBase
{
    public override async Task<ReserveStockResponse> ReserveStock(ReserveStockRequest request, ServerCallContext context)
    {
        logger.LogInformation("gRPC ReserveStock called for OrderId {OrderId}, SKU {Sku}, Qty {Qty}",
            request.OrderId, request.Sku, request.Quantity);

        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            return new ReserveStockResponse
            {
                IsSuccess = false,
                Message = "Invalid OrderId format"
            };
        }

        if (string.IsNullOrWhiteSpace(request.Sku) || request.Quantity <= 0)
        {
            return new ReserveStockResponse
            {
                IsSuccess = false,
                Message = "SKU is required and Quantity must be greater than zero"
            };
        }

        var result = await sender.Send(
            new ReserveStockCommand(orderId, request.Sku, request.Quantity),
            context.CancellationToken);
        return new ReserveStockResponse
        {
            ReservationId = result.ReservationId.ToString("D"),
            IsSuccess = result.Success,
            Message = result.Message
        };
    }

    public override async Task<ReleaseStockResponse> ReleaseStock(ReleaseStockRequest request, ServerCallContext context)
    {
        logger.LogInformation("gRPC ReleaseStock called for ReservationId {ReservationId}", request.ReservationId);

        if (!Guid.TryParse(request.ReservationId, out var reservationId))
        {
            return new ReleaseStockResponse
            {
                IsSuccess = false,
                Message = "Invalid ReservationId format"
            };
        }

        var released = await sender.Send(new ReleaseStockCommand(reservationId), context.CancellationToken);
        return new ReleaseStockResponse
        {
            IsSuccess = released,
            Message = released ? "Stock released successfully" : "Reservation not found"
        };
    }
}
