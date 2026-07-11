using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Domain.Entities;
using OrderService.Api.Domain.Enums;
using OrderService.Api.Infrastructure.Data;
using Shared.Events;
using Microsoft.Extensions.Logging;
using OrderService.Api.Application.Metrics;

namespace OrderService.Api.Application.Orders.Commands;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly OrderDbContext _dbContext;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ILogger<CreateOrderCommandHandler> _logger;
    private readonly OrderMetrics _metrics;

    public CreateOrderCommandHandler(OrderDbContext dbContext, ISendEndpointProvider sendEndpointProvider, ILogger<CreateOrderCommandHandler> logger, OrderMetrics metrics)
    {
        _dbContext = dbContext;
        _sendEndpointProvider = sendEndpointProvider;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Check for idempotency
        var existingOrder = await _dbContext.Orders
            .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existingOrder != null)
        {
            _logger.LogInformation("Idempotent request detected. Returning existing order id: {OrderId}", existingOrder.Id);
            return existingOrder.Id;
        }

        var totalAmount = request.Items.Sum(x => x.Quantity * x.UnitPrice);

        var order = new Order
        {
            CustomerId = request.CustomerId,
            IdempotencyKey = request.IdempotencyKey,
            TotalAmount = totalAmount,
            Status = OrderStatus.Pending,
            IsVip = request.IsVip,
            PaymentMethod = request.PaymentMethod,
            Items = request.Items.Select(x => new OrderItem
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice
            }).ToList()
        };

        _dbContext.Orders.Add(order);
        
        var orderCreatedEvent = new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
            request.IsVip,
            request.PaymentMethod
        );

        var queueName = request.IsVip ? "queue:vip-orders-queue" : "queue:orders-queue";
        var sendEndpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri(queueName));
        
        await sendEndpoint.Send(orderCreatedEvent, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Order {OrderId} created and event saved to Outbox.", order.Id);

        _metrics.OrderCreated();

        return order.Id;
    }
}
