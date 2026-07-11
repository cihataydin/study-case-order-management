using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentService.Api.Infrastructure.Data;
using PaymentService.Api.Domain.Enums;
using Shared.Events;
using PaymentService.Api.Domain.Entities;

namespace PaymentService.Api.Application.Consumers;

public class OrderCancelledEventConsumer : IConsumer<OrderCancelledEvent>
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<OrderCancelledEventConsumer> _logger;

    public OrderCancelledEventConsumer(PaymentDbContext dbContext, ILogger<OrderCancelledEventConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("OrderCancelledEvent received for OrderId: {OrderId}. Checking for refund...", message.OrderId);

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == message.OrderId);

        if (payment != null)
        {
            if (payment.Status != PaymentStatus.RefundPending && payment.Status != PaymentStatus.Reversed)
            {
                _logger.LogInformation("Initiating refund process for OrderId: {OrderId} amount: {Amount}", message.OrderId, payment.Amount);
                payment.Status = PaymentStatus.RefundPending;
                payment.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Payment marked for refund (RefundPending) for OrderId: {OrderId}", message.OrderId);
            }
        }
        else
        {
            // Payment doesn't exist yet, it means cancellation arrived before StockReservedEvent (or payment was never created).
            // We create a ghost record to block StockReservedEventConsumer from processing it.
            _logger.LogInformation("Payment not found for OrderId: {OrderId}. Creating ghost record to prevent future processing.", message.OrderId);
            payment = new Payment
            {
                OrderId = message.OrderId,
                Amount = 0,
                Method = "Unknown",
                Status = PaymentStatus.RefundPending, // Or Cancelled
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync();
        }
    }
}
