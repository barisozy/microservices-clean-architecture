using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Search.Domain.Entities;
using Search.Infrastructure.Data;

namespace Search.Infrastructure.Consumers;

public class ProductUpsertedConsumer : IConsumer<ProductUpserted>
{
    private readonly SearchDbContext _dbContext;
    private readonly ILogger<ProductUpsertedConsumer> _logger;

    public ProductUpsertedConsumer(SearchDbContext dbContext, ILogger<ProductUpsertedConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProductUpserted> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Consuming ProductUpserted event for SKU '{Sku}'", msg.Sku);

        var existing = await _dbContext.SearchIndices.FirstOrDefaultAsync(s => s.Sku == msg.Sku);
        if (existing == null)
        {
            _dbContext.SearchIndices.Add(new SearchIndex
            {
                Sku = msg.Sku,
                Name = msg.Name,
                Price = msg.Price,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Name = msg.Name;
            existing.Price = msg.Price;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
    }
}
