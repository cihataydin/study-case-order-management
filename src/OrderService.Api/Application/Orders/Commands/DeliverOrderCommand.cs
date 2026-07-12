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

public record DeliverOrderCommand(Guid OrderId) : IRequest<bool>;

public class DeliverOrderCommandHandler : IRequestHandler<DeliverOrderCommand, bool>
{
    private readonly OrderDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<DeliverOrderCommandHandler> _logger;

    public DeliverOrderCommandHandler(OrderDbContext dbContext, IPublishEndpoint publishEndpoint, ILogger<DeliverOrderCommandHandler> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> Handle(DeliverOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Order not found: {OrderId}", request.OrderId);
            return false;
        }

        order.Deliver();

        _logger.LogInformation("Order {OrderId} marked as delivered.", request.OrderId);
        await _publishEndpoint.Publish(new OrderDeliveredEvent(request.OrderId), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
