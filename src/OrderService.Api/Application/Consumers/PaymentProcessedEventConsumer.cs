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

public class PaymentProcessedEventConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly OrderDbContext _dbContext;
    private readonly ILogger<PaymentProcessedEventConsumer> _logger;
    private readonly OrderMetrics _metrics;

    public PaymentProcessedEventConsumer(OrderDbContext dbContext, ILogger<PaymentProcessedEventConsumer> logger, OrderMetrics metrics)
    {
        _dbContext = dbContext;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("PaymentProcessedEvent received for OrderId: {OrderId}", message.OrderId);

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == message.OrderId);
        if (order != null)
        {
            if (order.Status != OrderStatus.Pending)
            {
                _logger.LogWarning("Order {OrderId} is in {Status} status. Ignoring PaymentProcessedEvent.", message.OrderId, order.Status);

                if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Failed)
                {
                    await context.Publish(new OrderCancelledEvent(message.OrderId, "Late payment success for cancelled order"));
                }
                return;
            }

            order.Confirm();
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Order {OrderId} confirmed.", message.OrderId);
            
            _metrics.OrderSuccess(order.TotalAmount);

            await context.Publish(new OrderConfirmedEvent(message.OrderId));
        }
    }
}
