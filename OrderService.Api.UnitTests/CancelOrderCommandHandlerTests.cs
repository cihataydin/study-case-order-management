using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OrderService.Api.Application.Orders.Commands;
using OrderService.Api.Domain.Entities;
using OrderService.Api.Domain.Enums;
using OrderService.Api.Infrastructure.Data;
using Shared.Events;
using Xunit;

namespace OrderService.Api.UnitTests;

public class CancelOrderCommandHandlerTests : IDisposable
{
    private readonly OrderDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CancelOrderCommandHandler> _logger;
    private readonly CancelOrderCommandHandler _handler;

    public CancelOrderCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OrderDbContext(options);

        _publishEndpoint = Substitute.For<IPublishEndpoint>();
        _logger = Substitute.For<ILogger<CancelOrderCommandHandler>>();

        _handler = new CancelOrderCommandHandler(_dbContext, _publishEndpoint, _logger);
    }

    [Fact]
    public async Task Handle_WithValidPendingOrder_ShouldCancelAndPublishEvent()
    {
        // Arrange
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = "key1",
            TotalAmount = 200.00m,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var command = new CancelOrderCommand(order.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedOrder = await _dbContext.Orders.FindAsync(order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Cancelled, updatedOrder.Status);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<OrderCancelledEvent>(e => e.OrderId == order.Id && e.Reason == "User requested cancellation"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnFalse()
    {
        // Arrange
        var command = new CancelOrderCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);
        await _publishEndpoint.DidNotReceive().Publish(Arg.Any<OrderCancelledEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OrderOlderThanTwoHours_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = "key2",
            TotalAmount = 200.00m,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddHours(-3) // 3 hours ago
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var command = new CancelOrderCommand(order.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _handler.Handle(command, CancellationToken.None)
        );

        var unchangedOrder = await _dbContext.Orders.FindAsync(order.Id);
        Assert.NotNull(unchangedOrder);
        Assert.Equal(OrderStatus.Pending, unchangedOrder.Status);
        await _publishEndpoint.DidNotReceive().Publish(Arg.Any<OrderCancelledEvent>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered)]
    public async Task Handle_OrderAlreadyCancelledOrDelivered_ShouldReturnFalse(OrderStatus status)
    {
        // Arrange
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = "key3-" + status,
            TotalAmount = 200.00m,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var command = new CancelOrderCommand(order.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);
        await _publishEndpoint.DidNotReceive().Publish(Arg.Any<OrderCancelledEvent>(), Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
