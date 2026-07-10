using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventoryService.Api.Infrastructure.Data;
using Shared.Events;

namespace InventoryService.Api.Application.Consumers;

public class OrderConfirmedEventConsumer : IConsumer<OrderConfirmedEvent>
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<OrderConfirmedEventConsumer> _logger;

    public OrderConfirmedEventConsumer(InventoryDbContext dbContext, ILogger<OrderConfirmedEventConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("OrderConfirmedEvent received for OrderId: {OrderId}. Removing temporary reservations...", message.OrderId);

        var reservations = await _dbContext.StockReservations
            .Where(r => r.OrderId == message.OrderId)
            .ToListAsync();

        if (reservations.Any())
        {
            _dbContext.StockReservations.RemoveRange(reservations);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Temporary stock reservations removed for OrderId: {OrderId} as the order is confirmed.", message.OrderId);
        }
    }
}
