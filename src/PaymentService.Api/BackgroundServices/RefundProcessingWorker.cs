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

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task ProcessRefundsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var thresholdDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(5));

        var pendingRefunds = await dbContext.Payments
            .Where(p => p.Status == PaymentStatus.RefundPending && p.UpdatedAt <= thresholdDate)
            .Take(50)
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
