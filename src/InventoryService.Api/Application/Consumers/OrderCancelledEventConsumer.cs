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
            _dbContext.StockReservations.RemoveRange(reservations);
        }

        if (message.Items != null && message.Items.Any())
        {
            var productIds = message.Items.Select(i => i.ProductId).ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var item in message.Items)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    product.IncreaseStock(item.Quantity);
                    _logger.LogInformation("Released {Quantity} stock for ProductId: {ProductId}", item.Quantity, item.ProductId);
                }
            }
        }
        else if (reservations.Any())
        {
            var productIds = reservations.Select(r => r.ProductId).ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var reservation in reservations)
            {
                if (products.TryGetValue(reservation.ProductId, out var product))
                {
                    product.IncreaseStock(reservation.Quantity);
                    _logger.LogInformation("Released {Quantity} stock for ProductId: {ProductId} (fallback)", reservation.Quantity, reservation.ProductId);
                }
            }
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Stock reservations released for OrderId: {OrderId}", message.OrderId);
    }
}
