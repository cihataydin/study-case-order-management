using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Api.Domain.Enums;
using OrderService.Api.Infrastructure.Data;
using Shared.Events;

namespace OrderService.Api.Application.Consumers;

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
        var message = context.Message.Message;
        var exceptions = context.Message.Exceptions;
        
        _logger.LogError("DLQ Triggered: OrderCreatedEvent failed permanently for OrderId: {OrderId}. Exceptions: {Exceptions}", 
            message.OrderId, string.Join(", ", exceptions.Select(e => e.Message)));

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == message.OrderId);
        
        if (order != null && order.Status == OrderStatus.Pending)
        {
            order.MarkAsFailed();
            
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Order {OrderId} status updated to Cancelled due to DLQ fault.", message.OrderId);
        }
    }
}
