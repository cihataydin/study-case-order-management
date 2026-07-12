using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OrderService.Api.Application.Metrics;
using OrderService.Api.Application.Orders.Commands;
using OrderService.Api.Domain.Entities;
using OrderService.Api.Domain.Enums;
using OrderService.Api.Infrastructure.Data;
using Shared.Events;
using Shared.Enums;
using Shared.Exceptions;
using Xunit;

namespace OrderService.Api.UnitTests;

public class CreateOrderCommandHandlerTests : IDisposable
{
    private readonly OrderDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateOrderCommandHandler> _logger;
    private readonly OrderMetrics _metrics;
    private readonly IMeterFactory _meterFactory;
    private readonly Shared.Grpc.InventoryGrpcService.InventoryGrpcServiceClient _inventoryGrpcClient;
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OrderDbContext(options);

        _publishEndpoint = Substitute.For<IPublishEndpoint>();
        
        _logger = Substitute.For<ILogger<CreateOrderCommandHandler>>();

        _meterFactory = Substitute.For<IMeterFactory>();
        var meter = new Meter(OrderMetrics.MeterName);
        _meterFactory.Create(Arg.Any<MeterOptions>()).Returns(meter);
        _metrics = new OrderMetrics(_meterFactory);

        _inventoryGrpcClient = Substitute.For<Shared.Grpc.InventoryGrpcService.InventoryGrpcServiceClient>();
        _inventoryGrpcClient.GetProductPricesAsync(Arg.Any<Shared.Grpc.GetProductPricesRequest>(), Arg.Any<global::Grpc.Core.Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => 
            {
                var req = callInfo.Arg<Shared.Grpc.GetProductPricesRequest>();
                var res = new Shared.Grpc.GetProductPricesResponse();
                foreach (var id in req.ProductIds)
                {
                    res.Products.Add(new Shared.Grpc.ProductPriceItem { ProductId = id, Price = 150.0 });
                }
                
                return new global::Grpc.Core.AsyncUnaryCall<Shared.Grpc.GetProductPricesResponse>(
                    Task.FromResult(res),
                    Task.FromResult(new global::Grpc.Core.Metadata()),
                    () => global::Grpc.Core.Status.DefaultSuccess,
                    () => new global::Grpc.Core.Metadata(),
                    () => { });
            });

        _handler = new CreateOrderCommandHandler(_dbContext, _publishEndpoint, _logger, _metrics, _inventoryGrpcClient);
    }

    [Fact]
    public async Task Handle_WithNewRequest_ShouldCreateOrderAndPublishEvent()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: Guid.NewGuid().ToString(),
            Items: new List<CreateOrderItemDto>
            {
                new(Guid.NewGuid(), 2),
                new(Guid.NewGuid(), 1)
            },
            IsVip: false,
            PaymentMethod: PaymentMethod.CreditCard
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);

        var order = await _dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == result);
        Assert.NotNull(order);
        Assert.Equal(command.CustomerId, order.CustomerId);
        Assert.Equal(command.IdempotencyKey, order.IdempotencyKey);
        Assert.Equal(450.00m, order.TotalAmount); // 2 * 150 + 1 * 150
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, order.Items.Count);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<OrderCreatedEvent>(e =>
                e.OrderId == result &&
                e.CustomerId == command.CustomerId &&
                e.TotalAmount == 450.00m &&
                e.IsVip == false &&
                e.PaymentMethod == PaymentMethod.CreditCard &&
                e.Items.Count == 2
            ),
            Arg.Any<IPipe<PublishContext<OrderCreatedEvent>>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_WithExistingIdempotencyKey_ShouldThrowDomainException()
    {
        // Arrange
        var existingOrderId = Guid.NewGuid();
        var idempotencyKey = "duplicate-key-123";

        var existingOrder = new Order {
            Id = existingOrderId,
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            TotalAmount = 300.00m,
                    }.WithStatus(OrderStatus.Pending);
        _dbContext.Orders.Add(existingOrder);
        await _dbContext.SaveChangesAsync();

        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: idempotencyKey,
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1) },
            IsVip: false,
            PaymentMethod: PaymentMethod.CreditCard
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("already exists", ex.Message);
        
        var totalOrders = await _dbContext.Orders.CountAsync();
        Assert.Equal(1, totalOrders); // No new order inserted

        // Event should not be published again
        await _publishEndpoint.DidNotReceive().Publish(
            Arg.Any<OrderCreatedEvent>(),
            Arg.Any<IPipe<PublishContext<OrderCreatedEvent>>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_WithVipCustomer_ShouldPublishVipEvent()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: Guid.NewGuid().ToString(),
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1) },
            IsVip: true,
            PaymentMethod: PaymentMethod.Wallet
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _publishEndpoint.Received(1).Publish(
            Arg.Is<OrderCreatedEvent>(e =>
                e.OrderId == result &&
                e.IsVip == true &&
                e.PaymentMethod == PaymentMethod.Wallet
            ),
            Arg.Any<IPipe<PublishContext<OrderCreatedEvent>>>(),
            Arg.Any<CancellationToken>()
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
