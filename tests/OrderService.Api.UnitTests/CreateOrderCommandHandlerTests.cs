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
using Xunit;

namespace OrderService.Api.UnitTests;

public class CreateOrderCommandHandlerTests : IDisposable
{
    private readonly OrderDbContext _dbContext;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ISendEndpoint _sendEndpoint;
    private readonly ILogger<CreateOrderCommandHandler> _logger;
    private readonly OrderMetrics _metrics;
    private readonly IMeterFactory _meterFactory;
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OrderDbContext(options);

        _sendEndpointProvider = Substitute.For<ISendEndpointProvider>();
        _sendEndpoint = Substitute.For<ISendEndpoint>();
        _sendEndpointProvider.GetSendEndpoint(Arg.Any<Uri>()).Returns(_sendEndpoint);
        
        _logger = Substitute.For<ILogger<CreateOrderCommandHandler>>();

        _meterFactory = Substitute.For<IMeterFactory>();
        var meter = new Meter(OrderMetrics.MeterName);
        _meterFactory.Create(Arg.Any<MeterOptions>()).Returns(meter);
        _metrics = new OrderMetrics(_meterFactory);

        _handler = new CreateOrderCommandHandler(_dbContext, _sendEndpointProvider, _logger, _metrics);
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
                new(Guid.NewGuid(), 2, 150.00m),
                new(Guid.NewGuid(), 1, 200.00m)
            },
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);

        var order = await _dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == result);
        Assert.NotNull(order);
        Assert.Equal(command.CustomerId, order.CustomerId);
        Assert.Equal(command.IdempotencyKey, order.IdempotencyKey);
        Assert.Equal(500.00m, order.TotalAmount); // 2 * 150 + 1 * 200
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, order.Items.Count);

        await _sendEndpoint.Received(1).Send(
            Arg.Is<OrderCreatedEvent>(e =>
                e.OrderId == result &&
                e.CustomerId == command.CustomerId &&
                e.TotalAmount == 500.00m &&
                e.IsVip == false &&
                e.PaymentMethod == "CreditCard" &&
                e.Items.Count == 2
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_WithExistingIdempotencyKey_ShouldReturnExistingOrderIdWithoutRecreating()
    {
        // Arrange
        var existingOrderId = Guid.NewGuid();
        var idempotencyKey = "duplicate-key-123";

        var existingOrder = new Order
        {
            Id = existingOrderId,
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            TotalAmount = 300.00m,
            Status = OrderStatus.Pending
        };
        _dbContext.Orders.Add(existingOrder);
        await _dbContext.SaveChangesAsync();

        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: idempotencyKey,
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1, 300.00m) },
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(existingOrderId, result);
        
        var totalOrders = await _dbContext.Orders.CountAsync();
        Assert.Equal(1, totalOrders); // No new order inserted

        // Event should not be published again
        await _sendEndpoint.DidNotReceive().Send(
            Arg.Any<OrderCreatedEvent>(),
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
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1, 1000.00m) },
            IsVip: true,
            PaymentMethod: "Wallet"
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _sendEndpoint.Received(1).Send(
            Arg.Is<OrderCreatedEvent>(e =>
                e.OrderId == result &&
                e.IsVip == true &&
                e.PaymentMethod == "Wallet"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
