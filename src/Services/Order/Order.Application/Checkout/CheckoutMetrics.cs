using System.Diagnostics.Metrics;

namespace Order.Application.Checkout;

public class CheckoutMetrics
{
    public const string MeterName = "ECommerce.Order.Checkout";
    private readonly Meter _meter;
    private readonly Counter<long> _transitionCount;
    private readonly Counter<long> _compensationCount;
    private readonly Counter<long> _timeoutCount;
    private readonly Histogram<double> _transitionDuration;

    public CheckoutMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        _transitionCount = _meter.CreateCounter<long>("checkout.transitions", description: "Count of state transitions");
        _compensationCount = _meter.CreateCounter<long>("checkout.compensations", description: "Count of compensation actions triggered");
        _timeoutCount = _meter.CreateCounter<long>("checkout.timeouts", description: "Count of checkout timeouts");
        _transitionDuration = _meter.CreateHistogram<double>("checkout.transition.duration", unit: "ms", description: "Duration of state transitions");
    }

    public void RecordTransition(string state) => _transitionCount.Add(1, new KeyValuePair<string, object?>("state", state));
    public void RecordCompensation(string reason) => _compensationCount.Add(1, new KeyValuePair<string, object?>("reason", reason));
    public void RecordTimeout(string timeoutType) => _timeoutCount.Add(1, new KeyValuePair<string, object?>("type", timeoutType));
}