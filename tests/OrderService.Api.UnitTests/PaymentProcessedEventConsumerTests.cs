using System;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OrderService.Api.Application.Consumers;
using OrderService.Api.Application.Metrics;
using OrderService.Api.Domain.Entities;
using OrderService.Api.Domain.Enums;
using OrderService.Api.Infrastructure.Data;
using Shared.Events;
using Xunit;

namespace OrderService.Api.UnitTests;

public class PaymentProcessedEventConsumerTests : IDisposable
{
    private readonly OrderDbContext _dbContext;
    private readonly ILogger<PaymentProcessedEventConsumer> _logger;
    private readonly OrderMetrics _metrics;
    private readonly IMeterFactory _meterFactory;
    private readonly PaymentProcessedEventConsumer _consumer;

    public PaymentProcessedEventConsumerTests()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OrderDbContext(options);

        _logger = Substitute.For<ILogger<PaymentProcessedEventConsumer>>();
        _meterFactory = Substitute.For<IMeterFactory>();
        var meter = new Meter(OrderMetrics.MeterName);
        _meterFactory.Create(Arg.Any<MeterOptions>()).Returns(meter);
        _metrics = new OrderMetrics(_meterFactory);

        _consumer = new PaymentProcessedEventConsumer(_dbContext, _logger, _metrics);
    }

    [Fact]
    public async Task Consume_WithValidOrder_ShouldConfirmOrderAndPublishConfirmedEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var order = new Order {
            Id = orderId,
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = "key1",
            TotalAmount = 250.00m,
                    }.WithStatus(OrderStatus.Pending);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var message = new PaymentProcessedEvent(orderId, paymentId, false);
        var context = Substitute.For<ConsumeContext<PaymentProcessedEvent>>();
        context.Message.Returns(message);

        // Act
        await _consumer.Consume(context);

        // Assert
        var updatedOrder = await _dbContext.Orders.FindAsync(orderId);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Confirmed, updatedOrder.Status);

        await context.Received(1).Publish(
            Arg.Is<OrderConfirmedEvent>(e => e.OrderId == orderId),
            Arg.Any<System.Threading.CancellationToken>()
        );
    }

    [Fact]
    public async Task Consume_WithNonExistentOrder_ShouldDoNothing()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var message = new PaymentProcessedEvent(orderId, paymentId, false);
        var context = Substitute.For<ConsumeContext<PaymentProcessedEvent>>();
        context.Message.Returns(message);

        // Act
        await _consumer.Consume(context);

        // Assert
        await context.DidNotReceive().Publish(
            Arg.Any<OrderConfirmedEvent>(),
            Arg.Any<System.Threading.CancellationToken>()
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
