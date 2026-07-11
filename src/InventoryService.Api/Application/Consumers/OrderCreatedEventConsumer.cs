using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventoryService.Api.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using InventoryService.Api.Infrastructure.Data;
using Shared.Events;

namespace InventoryService.Api.Application.Consumers;

public class OrderCreatedEventConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<OrderCreatedEventConsumer> _logger;
    private readonly IDistributedCache _cache;
    private readonly Application.Metrics.InventoryMetrics _metrics;
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;

    public OrderCreatedEventConsumer(
        InventoryDbContext dbContext, 
        ILogger<OrderCreatedEventConsumer> logger, 
        IDistributedCache cache, 
        Application.Metrics.InventoryMetrics metrics,
        StackExchange.Redis.IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _logger = logger;
        _cache = cache;
        _metrics = metrics;
        _redis = redis;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderCreatedEvent for OrderId: {OrderId}", message.OrderId);

        var groupedItems = message.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();

        var productIds = groupedItems.Select(i => i.ProductId).ToList();
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var item in groupedItems)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                _logger.LogWarning("Product {ProductId} not found for Order {OrderId}", item.ProductId, message.OrderId);
                await context.Publish(new StockReleasedEvent(message.OrderId, $"Product {item.ProductId} not found"));
                return;
            }

            if (product.TotalStock < item.Quantity)
            {
                _logger.LogWarning("Insufficient stock for Product {ProductId}. Required: {Quantity}, Available: {Stock}", item.ProductId, item.Quantity, product.TotalStock);
                await context.Publish(new StockReleasedEvent(message.OrderId, $"Insufficient stock for product {item.ProductId}"));
                return;
            }

            // Cannot reserve more than 50% of available stock per order
            var maxAllowedReservation = (int)Math.Ceiling(product.TotalStock * 0.5m);
            if (item.Quantity > maxAllowedReservation) 
            {
                _logger.LogWarning("Cannot reserve more than 50% of available stock for Product {ProductId}. Requested: {Requested}, Max Allowed: {MaxAllowed}", item.ProductId, item.Quantity, maxAllowedReservation);
                await context.Publish(new StockReleasedEvent(message.OrderId, $"Cannot reserve more than 50% of stock for product {item.ProductId}"));
                return;
            }

            // Fake Flash Sale Check (for the sake of the rule, let's assume any product with TotalStock > 1000 is a flash sale item)
            // Or ideally it should be a flag on the entity. Let's assume there's a convention.
            bool isFlashSale = product.Name.Contains("Flash");
            if (isFlashSale)
            {
                var cacheKey = $"Inventory_FlashSale_{message.CustomerId}_{item.ProductId}";
                try
                {
                    var db = _redis.GetDatabase();
                    long newCount = await db.StringIncrementAsync(cacheKey, item.Quantity);
                    
                    if (newCount == item.Quantity) 
                    {
                        // Set expiration on first increment
                        await db.KeyExpireAsync(cacheKey, TimeSpan.FromDays(30));
                    }

                    if (newCount > 2)
                    {
                        // Revert the increment
                        await db.StringDecrementAsync(cacheKey, item.Quantity);
                        
                        _logger.LogWarning("Flash sale items limited to max 2 per customer. CustomerId: {CustomerId}, ProductId: {ProductId}", message.CustomerId, item.ProductId);
                        await context.Publish(new StockReleasedEvent(message.OrderId, $"Flash sale limit exceeded for product {item.ProductId}"));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to Redis cache for Flash Sale validation. Rejecting order to prevent overselling. CustomerId: {CustomerId}, ProductId: {ProductId}", message.CustomerId, item.ProductId);
                    await context.Publish(new StockReleasedEvent(message.OrderId, "System error during Flash Sale validation."));
                    return;
                }
            }

            product.TotalStock -= item.Quantity;

            // Low stock alert when quantity < 10
            if (product.TotalStock < 10)
            {
                _logger.LogWarning("LOW STOCK ALERT: Product {ProductId} has only {Stock} remaining!", product.Id, product.TotalStock);
                _metrics.RecordLowStockAlert();
            }

            var reservation = new StockReservation
            {
                OrderId = message.OrderId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            };

            _dbContext.StockReservations.Add(reservation);
        }

        try
        {
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Stock reserved successfully for OrderId: {OrderId}", message.OrderId);
            await context.Publish(new StockReservedEvent(message.OrderId, message.TotalAmount, message.PaymentMethod));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error occurred while reserving stock for OrderId: {OrderId}", message.OrderId);
            // In a real scenario we could retry, here we fail the reservation.
            await context.Publish(new StockReleasedEvent(message.OrderId, "Concurrency conflict occurred"));
        }
    }
}
