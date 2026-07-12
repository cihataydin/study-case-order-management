using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentService.Api.Domain.Enums;
using PaymentService.Api.Infrastructure.Data;

namespace PaymentService.Api.BackgroundServices;

/// <summary>
/// This BackgroundService simulates the 3-5 business days refund process asynchronously.
/// In a real production environment, this would process batches of pending refunds and 
/// communicate with the actual Bank/Payment Gateway APIs.
/// </summary>
public class RefundProcessingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RefundProcessingWorker> _logger;

    public RefundProcessingWorker(IServiceProvider serviceProvider, ILogger<RefundProcessingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RefundProcessingWorker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRefundsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing refunds.");
            }

            // In production, this would run maybe once a day. For simulation, every 30 seconds.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessRefundsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        // We simulate a 3-5 day wait by checking if the refund has been pending for more than a set time.
        // For testing/simulation purposes, we use a much shorter timespan (e.g., 1 minute).
        var thresholdDate = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1)); // SIMULATION: 1 minute instead of 3 days

        var pendingRefunds = await dbContext.Payments
            .Where(p => p.Status == PaymentStatus.RefundPending && p.UpdatedAt <= thresholdDate)
            .Take(50) // Batch size
            .ToListAsync(stoppingToken);

        if (pendingRefunds.Any())
        {
            _logger.LogInformation("Found {Count} pending refunds to process.", pendingRefunds.Count);

            foreach (var payment in pendingRefunds)
            {
                payment.Status = PaymentStatus.Reversed;
                payment.UpdatedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Refund successfully processed for PaymentId: {PaymentId}", payment.Id);
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}
