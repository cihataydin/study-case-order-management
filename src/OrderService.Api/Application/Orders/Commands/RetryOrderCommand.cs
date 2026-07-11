using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Infrastructure.Data;
using OrderService.Api.Domain.Enums;
using Shared.Events;
using Microsoft.Extensions.Logging;

namespace OrderService.Api.Application.Orders.Commands;

public record RetryOrderCommand(Guid OrderId) : IRequest<bool>;

public class RetryOrderCommandHandler : IRequestHandler<RetryOrderCommand, bool>
{
    private readonly OrderDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RetryOrderCommandHandler> _logger;

    public RetryOrderCommandHandler(OrderDbContext dbContext, IPublishEndpoint publishEndpoint, ILogger<RetryOrderCommandHandler> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> Handle(RetryOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null) return false;

        if (order.Status != OrderStatus.Failed)
        {
            _logger.LogWarning("Order {OrderId} is not Failed. Only failed orders can be retried.", request.OrderId);
            return false;
        }

        // Reset status to Pending and retry
        order.Status = OrderStatus.Pending;
        order.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Retrying OrderId: {OrderId}", request.OrderId);

        var orderCreatedEvent = new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
            false, // Simplification for retry
            "CreditCard" // Simplification for retry
        );

        await _publishEndpoint.Publish(orderCreatedEvent, cancellationToken);

        return true;
    }
}
