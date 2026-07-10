using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Api.Infrastructure.Data;
using OrderService.Api.Domain.Enums;
using Shared.Events;
using OrderService.Api.Application.Metrics;

namespace OrderService.Api.Application.Consumers;

public class StockReleasedEventConsumer : IConsumer<StockReleasedEvent>
{
    private readonly OrderDbContext _dbContext;
    private readonly ILogger<StockReleasedEventConsumer> _logger;
    private readonly OrderMetrics _metrics;

    public StockReleasedEventConsumer(OrderDbContext dbContext, ILogger<StockReleasedEventConsumer> logger, OrderMetrics metrics)
    {
        _dbContext = dbContext;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task Consume(ConsumeContext<StockReleasedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("StockReleasedEvent received for OrderId: {OrderId}. Reason: {Reason}", message.OrderId, message.Reason);

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == message.OrderId);
        if (order != null)
        {
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Order {OrderId} cancelled due to stock reservation failure or release.", message.OrderId);
            
            _metrics.OrderFailed();

            await context.Publish(new OrderCancelledEvent(message.OrderId, message.Reason));
        }
    }
}
