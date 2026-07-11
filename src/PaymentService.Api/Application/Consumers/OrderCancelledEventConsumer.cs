using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentService.Api.Infrastructure.Data;
using PaymentService.Api.Domain.Enums;
using Shared.Events;

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

        if (payment != null && payment.Status == PaymentStatus.Success)
        {
            _logger.LogInformation("Initiating refund process for OrderId: {OrderId} amount: {Amount}", message.OrderId, payment.Amount);
            payment.Status = PaymentStatus.RefundPending;
            payment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Payment marked for refund (RefundPending) for OrderId: {OrderId}", message.OrderId);
        }
    }
}
