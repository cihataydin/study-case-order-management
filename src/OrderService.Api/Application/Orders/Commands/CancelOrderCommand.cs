using System;
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

public record CancelOrderCommand(Guid OrderId) : IRequest<bool>;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly OrderDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(OrderDbContext dbContext, IPublishEndpoint publishEndpoint, ILogger<CancelOrderCommandHandler> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Order not found: {OrderId}", request.OrderId);
            return false;
        }

        // Rule: Order cancellation allowed within 2 hours
        if ((DateTime.UtcNow - order.CreatedAt).TotalHours > 2)
        {
            _logger.LogWarning("Order {OrderId} cannot be cancelled after 2 hours.", request.OrderId);
            throw new InvalidOperationException("Order cannot be cancelled after 2 hours.");
        }

        if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Shipped)
        {
            _logger.LogWarning("Order {OrderId} cannot be cancelled because it is in {Status} status.", request.OrderId, order.Status);
            return false;
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} cancelled by user.", request.OrderId);
        await _publishEndpoint.Publish(new OrderCancelledEvent(request.OrderId, "User requested cancellation"), cancellationToken);

        return true;
    }
}
