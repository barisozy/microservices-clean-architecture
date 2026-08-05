using System.Diagnostics;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Order.Application.Checkout;
using Order.Infrastructure.Data;

namespace Order.Infrastructure.BackgroundServices;

public class SagaReconciliationBackgroundService(IServiceProvider serviceProvider, ILogger<SagaReconciliationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                var stalledLimit = DateTimeOffset.UtcNow.AddHours(-1);
                var stalledSagas = await db.CheckoutStates
                    .Where(x => x.CurrentState != "Final" && x.StartedAt < stalledLimit)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var saga in stalledSagas)
                {
                    logger.LogWarning("Stalled checkout saga detected. OrderId: {OrderId}, State: {State}, StartedAt: {StartedAt}", 
                        saga.CorrelationId, saga.CurrentState, saga.StartedAt);

                    // Trigger cancellation to force compensations
                    await publishEndpoint.Publish(new OrderCancelled(saga.CorrelationId, "STALLED_SAGA_RECONCILIATION", DateTimeOffset.UtcNow), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in SagaReconciliationBackgroundService");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}