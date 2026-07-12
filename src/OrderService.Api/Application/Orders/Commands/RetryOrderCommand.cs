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
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ILogger<RetryOrderCommandHandler> _logger;

    public RetryOrderCommandHandler(OrderDbContext dbContext, ISendEndpointProvider sendEndpointProvider, ILogger<RetryOrderCommandHandler> logger)
    {
        _dbContext = dbContext;
        _sendEndpointProvider = sendEndpointProvider;
        _logger = logger;
    }

    public async Task<bool> Handle(RetryOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null) return false;

        order.Retry();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Retrying OrderId: {OrderId}", request.OrderId);

        var orderCreatedEvent = new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
            order.IsVip,
            order.PaymentMethod
        );

        var queueName = order.IsVip ? "queue:vip-orders-queue" : "queue:orders-queue";
        var sendEndpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri(queueName));
        
        await sendEndpoint.Send(orderCreatedEvent, cancellationToken);

        return true;
    }
}
