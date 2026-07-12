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
using Shared.Exceptions;

namespace InventoryService.Api.Application.Consumers;

public class OrderCreatedEventConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<OrderCreatedEventConsumer> _logger;
    private readonly IDistributedCache _cache;
    private readonly Application.Metrics.InventoryMetrics _metrics;
    private readonly Application.Services.IFlashSaleService _flashSaleService;

    public OrderCreatedEventConsumer(
        InventoryDbContext dbContext, 
        ILogger<OrderCreatedEventConsumer> logger, 
        IDistributedCache cache, 
        Application.Metrics.InventoryMetrics metrics,
        Application.Services.IFlashSaleService flashSaleService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _cache = cache;
        _metrics = metrics;
        _flashSaleService = flashSaleService;
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
                await context.Publish(new StockReleasedEvent(message.OrderId, $"Product {item.ProductId} not found", message.IsVip));
                return;
            }

            var (isAllowed, flashSaleError) = await _flashSaleService.CheckFlashSaleLimitAsync(message.CustomerId, item.ProductId, item.Quantity, product.Name);
            if (!isAllowed)
            {
                _logger.LogWarning(flashSaleError);
                await context.Publish(new StockReleasedEvent(message.OrderId, flashSaleError, message.IsVip));
                return;
            }

            try
            {
                product.DecreaseStock(item.Quantity, applyReservationLimit: true);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Reservation rule violated for Product {ProductId}", item.ProductId);
                await context.Publish(new StockReleasedEvent(message.OrderId, ex.Message, message.IsVip));
                return;
            }

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
            await context.Publish(new StockReservedEvent(message.OrderId, message.TotalAmount, message.PaymentMethod, message.IsVip));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error occurred while reserving stock for OrderId: {OrderId}", message.OrderId);
            await context.Publish(new StockReleasedEvent(message.OrderId, "Concurrency conflict occurred", message.IsVip));
        }
    }
}
