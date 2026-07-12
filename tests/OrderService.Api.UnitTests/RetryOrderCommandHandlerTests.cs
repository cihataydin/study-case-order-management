using System;
using System.Collections.Generic;
using System.Linq;
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
using Shared.Enums;
using MediatR;
using Shared.Exceptions;
using Xunit;

namespace OrderService.Api.UnitTests;

public class RetryOrderCommandHandlerTests : IDisposable
{
    private readonly OrderDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RetryOrderCommandHandler> _logger;
    private readonly RetryOrderCommandHandler _handler;

    public RetryOrderCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OrderDbContext(options);

        _publishEndpoint = Substitute.For<IPublishEndpoint>();
        _logger = Substitute.For<ILogger<RetryOrderCommandHandler>>();

        _handler = new RetryOrderCommandHandler(_dbContext, _publishEndpoint, _logger);
    }

    [Fact]
    public async Task Handle_WithFailedOrder_ShouldResetToPendingAndPublishCreatedEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = "retry-key",
            TotalAmount = 250.00m,
            /* Status = OrderStatus.Failed */
            IsVip = false,
            PaymentMethod = PaymentMethod.CreditCard,
            Items = new List<OrderItem>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 125.00m }
            }
        }.WithStatus(OrderStatus.Failed);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var command = new RetryOrderCommand(orderId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedOrder = await _dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Pending, updatedOrder.Status);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<OrderCreatedEvent>(e =>
                e.OrderId == orderId &&
                e.CustomerId == order.CustomerId &&
                e.TotalAmount == 250.00m &&
                e.IsVip == false &&
                e.PaymentMethod == PaymentMethod.CreditCard &&
                e.Items.Count == 1
            ),
            Arg.Any<IPipe<PublishContext<OrderCreatedEvent>>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnFalse()
    {
        // Arrange
        var command = new RetryOrderCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);
        await _publishEndpoint.DidNotReceive().Publish(
            Arg.Any<OrderCreatedEvent>(),
            Arg.Any<IPipe<PublishContext<OrderCreatedEvent>>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Handle_WithNonFailedOrder_ShouldReturnFalse(OrderStatus status)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order {
            Id = orderId,
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = "retry-key-" + status,
            TotalAmount = 250.00m,
                    }.WithStatus(status);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var command = new RetryOrderCommand(orderId);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(async () =>
            await _handler.Handle(command, CancellationToken.None)
        );

        await _publishEndpoint.DidNotReceive().Publish(
            Arg.Any<OrderCreatedEvent>(),
            Arg.Any<IPipe<PublishContext<OrderCreatedEvent>>>(),
            Arg.Any<CancellationToken>()
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
