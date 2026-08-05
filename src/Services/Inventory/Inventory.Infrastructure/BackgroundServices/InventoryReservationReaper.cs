using ECommerce.Contracts.Events.v1;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.BackgroundServices;

public sealed class InventoryReservationReaper(
    IServiceScopeFactory scopeFactory,
    IOptions<InventoryReservationOptions> options,
    TimeProvider timeProvider,
    ILogger<InventoryReservationReaper> logger) : BackgroundService
{
    private readonly InventoryReservationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.ReaperInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ExpireBatch(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Inventory reservation reaper failed"); }
        }
    }

    private async Task ExpireBatch(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var now = timeProvider.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        
        var reservations = await db.Reservations
            .FromSqlInterpolated($"""
                SELECT *
                FROM inventory."InventoryReservations"
                WHERE "Status" = {(int)InventoryReservationStatus.Pending}
                  AND "ExpiresAt" <= {now}
                ORDER BY "ExpiresAt"
                FOR UPDATE SKIP LOCKED
                LIMIT {_options.ReaperBatchSize}
                """)
            .Include(r => r.Items)
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            if (!reservation.Expire(now)) continue;
            
            foreach(var item in reservation.Items)
            {
                var stock = await db.Stocks.FirstOrDefaultAsync(x => x.Sku == item.Sku, cancellationToken)
                    ?? throw new InvalidOperationException($"Stock '{item.Sku}' is missing for reservation '{reservation.Id}'.");
                stock.Release(item.Quantity);
            }
            
            await publisher.Publish(new InventoryReservationExpired(reservation.OrderId, now), publishContext => publishContext.CorrelationId = reservation.OrderId, cancellationToken);
            logger.LogWarning("Inventory reservation expired. OrderId={OrderId} ReservationId={ReservationId} ExpiresAt={ExpiresAt}", reservation.OrderId, reservation.Id, reservation.ExpiresAt);
        }

        if (reservations.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed class InventoryReservationOptions
{
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan ReaperInterval { get; set; } = TimeSpan.FromSeconds(15);
    public int ReaperBatchSize { get; set; } = 100;
}
