using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InventoryService.Api.Infrastructure.Data;

namespace InventoryService.Api.Application.Workers;

public class StockReservationCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StockReservationCleanupWorker> _logger;

    public StockReservationCleanupWorker(IServiceProvider serviceProvider, ILogger<StockReservationCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StockReservationCleanupWorker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredReservationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing StockReservationCleanupWorker.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("StockReservationCleanupWorker is stopping.");
    }

    private async Task CleanupExpiredReservationsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<MassTransit.IPublishEndpoint>();

        var expiredReservations = await dbContext.StockReservations
            .Where(r => r.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(stoppingToken);

        if (!expiredReservations.Any())
            return;

        _logger.LogInformation("Found {Count} expired stock reservations. Releasing them...", expiredReservations.Count);

        var productIds = expiredReservations.Select(r => r.ProductId).Distinct().ToList();
        var products = await dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, stoppingToken);

        foreach (var reservation in expiredReservations)
        {
            if (products.TryGetValue(reservation.ProductId, out var product))
            {
                product.IncreaseStock(reservation.Quantity);
                _logger.LogInformation("Released {Quantity} stock back to Product {ProductId} from expired reservation for Order {OrderId}", 
                    reservation.Quantity, reservation.ProductId, reservation.OrderId);
            }
        }

        dbContext.StockReservations.RemoveRange(expiredReservations);
        await dbContext.SaveChangesAsync(stoppingToken);

        var orderIds = expiredReservations.Select(r => r.OrderId).Distinct().ToList();
        foreach (var orderId in orderIds)
        {
            await publishEndpoint.Publish(new Shared.Events.StockReleasedEvent(orderId, "Reservation expired after 10 minutes"), stoppingToken);
        }
    }
}
