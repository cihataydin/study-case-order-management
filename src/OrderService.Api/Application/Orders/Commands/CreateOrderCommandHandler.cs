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
using Shared.Grpc;

namespace OrderService.Api.Application.Orders.Commands;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly OrderDbContext _dbContext;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ILogger<CreateOrderCommandHandler> _logger;
    private readonly OrderMetrics _metrics;
    private readonly InventoryGrpcService.InventoryGrpcServiceClient _inventoryGrpcClient;

    public CreateOrderCommandHandler(
        OrderDbContext dbContext, 
        ISendEndpointProvider sendEndpointProvider, 
        ILogger<CreateOrderCommandHandler> logger, 
        OrderMetrics metrics,
        InventoryGrpcService.InventoryGrpcServiceClient inventoryGrpcClient)
    {
        _dbContext = dbContext;
        _sendEndpointProvider = sendEndpointProvider;
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
            _logger.LogInformation("Idempotent request detected. Returning existing order id: {OrderId}", existingOrder.Id);
            return existingOrder.Id;
        }

        // Fetch prices from Inventory via gRPC
        var grpcRequest = new GetProductPricesRequest();
        grpcRequest.ProductIds.AddRange(request.Items.Select(i => i.ProductId.ToString()));

        var grpcResponse = await _inventoryGrpcClient.GetProductPricesAsync(grpcRequest, cancellationToken: cancellationToken);

        var priceDictionary = grpcResponse.Products.ToDictionary(
            p => Guid.Parse(p.ProductId), 
            p => (decimal)p.Price
        );

        decimal totalAmount = 0;
        var orderItems = new System.Collections.Generic.List<OrderItem>();

        foreach (var item in request.Items)
        {
            if (!priceDictionary.TryGetValue(item.ProductId, out var currentPrice))
            {
                throw new Exception($"Product {item.ProductId} not found in inventory!");
            }

            totalAmount += (item.Quantity * currentPrice);
            
            orderItems.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = currentPrice // Use the actual price from Inventory!
            });
        }

        var order = new Order
        {
            CustomerId = request.CustomerId,
            IdempotencyKey = request.IdempotencyKey,
            TotalAmount = totalAmount,
            Status = OrderStatus.Pending,
            IsVip = request.IsVip,
            PaymentMethod = request.PaymentMethod,
            Items = orderItems
        };

        _dbContext.Orders.Add(order);

        await _dbContext.SaveChangesAsync(cancellationToken);
        
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

        _logger.LogInformation("Order {OrderId} created and event saved to Outbox.", order.Id);

        _metrics.OrderCreated();

        return order.Id;
    }
}
