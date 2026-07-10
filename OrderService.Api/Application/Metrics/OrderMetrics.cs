using System.Diagnostics.Metrics;

namespace OrderService.Api.Application.Metrics;

public class OrderMetrics
{
    public const string MeterName = "Ecommerce.OrderService";
    private readonly Meter _meter;
    private readonly Counter<int> _ordersCreatedCounter;
    private readonly Counter<decimal> _revenueCounter;
    private readonly Counter<int> _orderSuccessCounter;
    private readonly Counter<int> _orderFailedCounter;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        _ordersCreatedCounter = _meter.CreateCounter<int>("orders.created.count", description: "Number of created orders");
        _revenueCounter = _meter.CreateCounter<decimal>("revenue.total", description: "Total revenue amount from confirmed orders");
        _orderSuccessCounter = _meter.CreateCounter<int>("orders.success.count", description: "Number of successfully processed orders");
        _orderFailedCounter = _meter.CreateCounter<int>("orders.failed.count", description: "Number of failed orders");
    }

    public void OrderCreated() => _ordersCreatedCounter.Add(1);
    public void OrderSuccess(decimal amount)
    {
        _orderSuccessCounter.Add(1);
        _revenueCounter.Add(amount);
    }
    public void OrderFailed() => _orderFailedCounter.Add(1);
}
