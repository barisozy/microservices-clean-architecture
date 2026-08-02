using System.Diagnostics;
using System.Diagnostics.Metrics;
using Catalog.Infrastructure.Data;
using ECommerce.Contracts.Protos;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class CatalogGrpcService : CatalogService.CatalogServiceBase
{
    private static readonly Meter Meter = new("Catalog.Api");
    private static readonly Histogram<double> PriceSnapshotDuration =
        Meter.CreateHistogram<double>("catalog.price_snapshot.duration", "ms");
    private readonly CatalogDbContext _dbContext;
    private readonly ILogger<CatalogGrpcService> _logger;

    public CatalogGrpcService(CatalogDbContext dbContext, ILogger<CatalogGrpcService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task<GetPriceSnapshotResponse> GetPriceSnapshot(GetPriceSnapshotRequest request, ServerCallContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;
        _logger.LogInformation("Getting price snapshot for SKU '{Sku}'", request.Sku);

        var product = await _dbContext.Products.FirstOrDefaultAsync(
            p => p.Sku == request.Sku,
            cancellationToken);
        if (product == null)
        {
            return new GetPriceSnapshotResponse { UnitPrice = 0.0, Available = false };
        }

        return new GetPriceSnapshotResponse
        {
            UnitPrice = (double)product.Price,
            Available = true
        };
        }
        finally
        {
            PriceSnapshotDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
