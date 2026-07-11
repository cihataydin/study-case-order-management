using System.Diagnostics.Metrics;

namespace InventoryService.Api.Application.Metrics;

public class InventoryMetrics
{
    public const string MeterName = "Ecommerce.InventoryService";
    private readonly Meter _meter;
    private readonly Counter<int> _lowStockAlertsCounter;

    public InventoryMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        _lowStockAlertsCounter = _meter.CreateCounter<int>("inventory.low.stock.alerts.total", description: "Number of low stock alerts triggered");
    }

    public void RecordLowStockAlert() => _lowStockAlertsCounter.Add(1);
}
