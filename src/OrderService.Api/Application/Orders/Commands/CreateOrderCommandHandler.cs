using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MediatR;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Domain.Entities;
using OrderService.Api.Domain.Enums;
using OrderService.Api.Infrastructure.Data;
using Shared.Events;
using Microsoft.Extensions.Logging;
using OrderService.Api.Application.Metrics;
using Shared.Grpc;

namespace OrderService.Api.Application.Orders.Commands;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly OrderDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateOrderCommandHandler> _logger;
    private readonly OrderMetrics _metrics;
    private readonly InventoryGrpcService.InventoryGrpcServiceClient _inventoryGrpcClient;

    public CreateOrderCommandHandler(
        OrderDbContext dbContext, 
        IPublishEndpoint publishEndpoint, 
        ILogger<CreateOrderCommandHandler> logger, 
        OrderMetrics metrics,
        InventoryGrpcService.InventoryGrpcServiceClient inventoryGrpcClient)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _metrics = metrics;
        _inventoryGrpcClient = inventoryGrpcClient;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var existingOrder = await _dbContext.Orders
            .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existingOrder != null)
        {
            _logger.LogWarning("Idempotent request detected. Order already exists with id: {OrderId}", existingOrder.Id);
            throw new InvalidOperationException($"An order with idempotency key '{request.IdempotencyKey}' already exists.");
        }

        var grpcRequest = new GetProductPricesRequest();
        grpcRequest.ProductIds.AddRange(request.Items.Select(i => i.ProductId.ToString()));

        var grpcResponse = await _inventoryGrpcClient.GetProductPricesAsync(grpcRequest, cancellationToken: cancellationToken);

        var priceDictionary = grpcResponse.Products.ToDictionary(
            p => Guid.Parse(p.ProductId), 
            p => (decimal)p.Price
        );

        var orderItems = new List<OrderItem>();

        foreach (var item in request.Items)
        {
            if (!priceDictionary.TryGetValue(item.ProductId, out var currentPrice))
            {
                throw new Exception($"Product {item.ProductId} not found in inventory!");
            }

            orderItems.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = currentPrice
            });
        }

        var order = Order.Create(
            request.CustomerId,
            request.IdempotencyKey,
            request.IsVip,
            request.PaymentMethod,
            orderItems
        );

        _dbContext.Orders.Add(order);

        var orderCreatedEvent = new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
            request.IsVip,
            request.PaymentMethod
        );

        await _publishEndpoint.Publish(orderCreatedEvent, context => 
        {
            if (request.IsVip)
            {
                context.SetPriority(9);
            }
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} created and event saved to Outbox.", order.Id);

        _metrics.OrderCreated();

        return order.Id;
    }
}
