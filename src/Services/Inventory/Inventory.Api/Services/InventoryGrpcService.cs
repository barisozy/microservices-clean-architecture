using ECommerce.Contracts.Protos;
using Grpc.Core;
using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Inventory.Application.Inventory.Commands;
using MediatR;

namespace Inventory.Api.Services;

public class InventoryGrpcService(
    IInventoryDbContext dbContext,
    ILogger<InventoryGrpcService> logger,
    ISender? sender = null) : InventoryService.InventoryServiceBase
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

        if (sender is not null)
        {
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

        var reservation = InventoryReservation.Create(orderId, request.Sku, request.Quantity);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        return new ReserveStockResponse
        {
            ReservationId = reservation.Id.ToString(),
            IsSuccess = true,
            Message = "Stock reserved successfully"
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

        if (sender is not null)
        {
            var released = await sender.Send(new ReleaseStockCommand(reservationId), context.CancellationToken);
            return new ReleaseStockResponse
            {
                IsSuccess = released,
                Message = released ? "Stock released successfully" : "Reservation not found"
            };
        }

        var reservation = await dbContext.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId, context.CancellationToken);

        if (reservation == null)
        {
            return new ReleaseStockResponse
            {
                IsSuccess = false,
                Message = "Reservation not found"
            };
        }

        reservation.Release();
        await dbContext.SaveChangesAsync(context.CancellationToken);

        return new ReleaseStockResponse
        {
            IsSuccess = true,
            Message = "Stock released successfully"
        };
    }
}
