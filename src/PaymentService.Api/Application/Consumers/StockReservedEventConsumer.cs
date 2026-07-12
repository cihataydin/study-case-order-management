using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentService.Api.Domain.Entities;
using PaymentService.Api.Domain.Enums;
using PaymentService.Api.Infrastructure.Data;
using Shared.Events;
using Polly;
using Polly.Retry;
using Microsoft.EntityFrameworkCore;

namespace PaymentService.Api.Application.Consumers;

public class StockReservedEventConsumer : IConsumer<StockReservedEvent>
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<StockReservedEventConsumer> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    public static Random RandomGenerator = new();

    public StockReservedEventConsumer(PaymentDbContext dbContext, ILogger<StockReservedEventConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<TimeoutException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning($"Payment simulation timeout. Retrying... Attempt {retryCount}");
                });
    }

    public async Task Consume(ConsumeContext<StockReservedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing payment for OrderId: {OrderId}", message.OrderId);

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == message.OrderId);

        if (payment != null)
        {
            _logger.LogWarning("Payment already exists for OrderId: {OrderId} with status {Status}. It may have been cancelled.", message.OrderId, payment.Status);
            return;
        }

        payment = new Payment
        {
            OrderId = message.OrderId,
            Amount = message.TotalAmount,
            Method = message.PaymentMethod ?? "CreditCard", // Fallback to CreditCard if null
            Status = PaymentStatus.Pending
        };

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        try
        {
            var isSuccess = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await SimulatePaymentAsync(message.TotalAmount);
            });

            if (isSuccess)
            {
                payment.Status = PaymentStatus.Success;
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Payment processed successfully for OrderId: {OrderId}", message.OrderId);
                await context.Publish(new PaymentProcessedEvent(message.OrderId, payment.Id, message.IsVip));
            }
            else
            {
                payment.Status = PaymentStatus.Failed;
                await _dbContext.SaveChangesAsync();
                
                _logger.LogWarning("Payment failed for OrderId: {OrderId}", message.OrderId);
                await context.Publish(new PaymentFailedEvent(message.OrderId, "Card declined by bank", message.IsVip));
            }
        }
        catch (Exception ex)
        {
            payment.Status = PaymentStatus.Failed;
            await _dbContext.SaveChangesAsync();
            
            _logger.LogError(ex, "Payment process failed completely for OrderId: {OrderId}", message.OrderId);
            await context.Publish(new PaymentFailedEvent(message.OrderId, "Payment gateway unavailable", message.IsVip));
        }
    }

    private async Task<bool> SimulatePaymentAsync(decimal amount)
    {
        // Fraud detection simulation (Orders > 10,000 TL require additional verification)
        if (amount > 10000)
        {
            _logger.LogInformation("Simulating additional fraud verification...");
            await Task.Delay(1000); // simulate verification time
        }

        var chance = RandomGenerator.Next(1, 101);
        
        // %85 success, %10 timeout, %5 failure
        if (chance <= 85)
        {
            return true;
        }
        else if (chance <= 95)
        {
            throw new TimeoutException("Simulated payment timeout");
        }
        else
        {
            return false; // Simulated failure
        }
    }
}
