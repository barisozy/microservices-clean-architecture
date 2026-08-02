using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Search.Infrastructure.Consumers;
using Search.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace Search.UnitTests;

public class ProductUpsertedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldCreateThenUpdateSearchDocument()
    {
        await using var db = new SearchTestDbContext(new DbContextOptionsBuilder<SearchDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var consumer = new ProductUpsertedConsumer(db, NullLogger<ProductUpsertedConsumer>.Instance);
        var context = new Mock<ConsumeContext<ProductUpserted>>();
        context.SetupGet(x => x.Message).Returns(new ProductUpserted("SKU-1", "First", 10m));
        await consumer.Consume(context.Object);
        context.SetupGet(x => x.Message).Returns(new ProductUpserted("SKU-1", "Updated", 12m));
        await consumer.Consume(context.Object);

        db.SearchIndices.Count().ShouldBe(1);
        db.SearchIndices.Single().Name.ShouldBe("Updated");
        db.SearchIndices.Single().Price.ShouldBe(12m);
        db.SearchIndices.Single().Sku.ShouldBe("SKU-1");
        db.SearchIndices.Single().UpdatedAt.ShouldBeGreaterThan(DateTime.MinValue);
    }

    private sealed class SearchTestDbContext(DbContextOptions<SearchDbContext> options) : SearchDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Search.Domain.Entities.SearchIndex>().Ignore("SearchVector");
        }
    }
}
