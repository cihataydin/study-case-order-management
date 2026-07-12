using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Api.Domain.Enums;
using OrderService.Api.Infrastructure.Data;
using Shared.Events;

namespace OrderService.Api.Application.Consumers;

/// <summary>
/// This consumer acts as a Dead Letter Queue (DLQ) handler.
/// If InventoryService or any other consumer permanently fails to process OrderCreatedEvent
/// (after all retries are exhausted) and the message is moved to the _error queue,
/// MassTransit automatically publishes a Fault<OrderCreatedEvent>.
/// We capture this fault here to formally cancel the order and alert the system.
/// </summary>
public class OrderCreatedEventFaultConsumer : IConsumer<Fault<OrderCreatedEvent>>
{
    private readonly OrderDbContext _dbContext;
    private readonly ILogger<OrderCreatedEventFaultConsumer> _logger;

    public OrderCreatedEventFaultConsumer(OrderDbContext dbContext, ILogger<OrderCreatedEventFaultConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<Fault<OrderCreatedEvent>> context)
    {
        var message = context.Message.Message; // The original event that failed
        var exceptions = context.Message.Exceptions; // The exceptions that caused the failure
        
        _logger.LogError("DLQ Triggered: OrderCreatedEvent failed permanently for OrderId: {OrderId}. Exceptions: {Exceptions}", 
            message.OrderId, string.Join(", ", exceptions.Select(e => e.Message)));

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == message.OrderId);
        
        if (order != null && order.Status == OrderStatus.Pending)
        {
            order.MarkAsFailed();
            
            // Note: We could save the exact reason to a new property like order.FailureReason if it existed in the domain
            
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Order {OrderId} status updated to Cancelled due to DLQ fault.", message.OrderId);
        }
    }
}
