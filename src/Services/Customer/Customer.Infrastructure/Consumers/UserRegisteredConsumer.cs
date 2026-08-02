using System.Diagnostics;
using System.Diagnostics.Metrics;
using Customer.Domain.Entities;
using Customer.Infrastructure.Data;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Customer.Infrastructure.Consumers;

public class UserRegisteredConsumer : IConsumer<UserRegistered>
{
    private static readonly Meter Meter = new("Customer.Api");
    private static readonly Histogram<double> ProfileSyncDuration =
        Meter.CreateHistogram<double>("customer.profile_sync.duration", "ms");
    private readonly CustomerDbContext _dbContext;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(CustomerDbContext dbContext, ILogger<UserRegisteredConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserRegistered> context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
        var msg = context.Message;
        _logger.LogInformation("Consuming UserRegistered event for subject '{Subject}'", msg.KeycloakSubject);

        var existing = await _dbContext.Profiles.FirstOrDefaultAsync(
            p => p.KeycloakSubject == msg.KeycloakSubject,
            context.CancellationToken);
        if (existing == null)
        {
            _dbContext.Profiles.Add(new CustomerProfile
            {
                KeycloakSubject = msg.KeycloakSubject,
                Email = msg.Email,
                DisplayName = msg.Email.Split('@')[0]
            });
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        }
        finally
        {
            ProfileSyncDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
