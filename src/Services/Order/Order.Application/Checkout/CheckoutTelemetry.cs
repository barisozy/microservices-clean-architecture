using System.Diagnostics.Metrics;

namespace Order.Application.Checkout;

internal sealed class CheckoutTelemetry
{
    private static readonly Meter Meter = new("Order.Api");
    private static readonly Histogram<double> CheckoutDuration = Meter.CreateHistogram<double>("order.checkout.duration", "ms");
    private static readonly Histogram<double> CatalogDuration = Meter.CreateHistogram<double>("catalog.price_snapshot.duration", "ms");
    private static readonly Histogram<double> PromotionDuration = Meter.CreateHistogram<double>("promotion.coupon_apply.duration", "ms");

    public IDisposable Checkout() => new DurationScope(CheckoutDuration);
    public IDisposable Catalog() => new DurationScope(CatalogDuration);
    public IDisposable Promotion() => new DurationScope(PromotionDuration);

    private sealed class DurationScope(Histogram<double> histogram) : IDisposable
    {
        private readonly long _startedAt = TimeProvider.System.GetTimestamp();
        public void Dispose() => histogram.Record(TimeProvider.System.GetElapsedTime(_startedAt).TotalMilliseconds);
    }
}
