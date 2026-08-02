using System.Diagnostics;
using System.Diagnostics.Metrics;
using Catalog.Infrastructure.Data;
using Catalog.Application;
using ECommerce.Contracts.Protos;
using FluentValidation;
using Grpc.Core;
using MediatR;

namespace Catalog.Api.Services;

public class CatalogGrpcService : CatalogService.CatalogServiceBase
{
    private static readonly Meter Meter = new("Catalog.Api");
    private static readonly Histogram<double> PriceSnapshotDuration =
        Meter.CreateHistogram<double>("catalog.price_snapshot.duration", "ms");
    private readonly ISender _sender;
    private readonly IValidator<GetProductQuery> _validator;
    private readonly ILogger<CatalogGrpcService> _logger;

    public CatalogGrpcService(
        ISender sender,
        IValidator<GetProductQuery> validator,
        ILogger<CatalogGrpcService> logger)
    {
        _sender = sender;
        _validator = validator;
        _logger = logger;
    }

    public override async Task<GetPriceSnapshotResponse> GetPriceSnapshot(GetPriceSnapshotRequest request, ServerCallContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;
        _logger.LogInformation("Getting price snapshot for SKU '{Sku}'", request.Sku);

        var query = new GetProductQuery(request.Sku);
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return new GetPriceSnapshotResponse { UnitPrice = 0.0, Available = false };
        var product = await _sender.Send(query, cancellationToken);
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
