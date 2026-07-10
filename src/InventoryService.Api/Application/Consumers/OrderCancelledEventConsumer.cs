using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventoryService.Api.Infrastructure.Data;
using Shared.Events;

namespace InventoryService.Api.Application.Consumers;

public class OrderCancelledEventConsumer : IConsumer<OrderCancelledEvent>
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<OrderCancelledEventConsumer> _logger;

    public OrderCancelledEventConsumer(InventoryDbContext dbContext, ILogger<OrderCancelledEventConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("OrderCancelledEvent received for OrderId: {OrderId}. Releasing stock...", message.OrderId);

        var reservations = await _dbContext.StockReservations
            .Where(r => r.OrderId == message.OrderId)
            .ToListAsync();

        if (reservations.Any())
        {
            var productIds = reservations.Select(r => r.ProductId).ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var reservation in reservations)
            {
                if (products.TryGetValue(reservation.ProductId, out var product))
                {
                    product.TotalStock += reservation.Quantity;
                    _logger.LogInformation("Released {Quantity} stock for ProductId: {ProductId}", reservation.Quantity, reservation.ProductId);
                }
            }

            _dbContext.StockReservations.RemoveRange(reservations);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Stock reservations released for OrderId: {OrderId}", message.OrderId);
        }
    }
}
