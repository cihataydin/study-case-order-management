using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using InventoryService.Api.Application.Consumers;
using InventoryService.Api.Domain.Entities;
using InventoryService.Api.Infrastructure.Data;
using Shared.Events;
using Xunit;

namespace InventoryService.Api.UnitTests;

public class InventoryConsumersTests : IDisposable
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<OrderCreatedEventConsumer> _createdLogger;
    private readonly ILogger<OrderCancelledEventConsumer> _cancelledLogger;
    private readonly ILogger<OrderConfirmedEventConsumer> _confirmedLogger;

    public InventoryConsumersTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new InventoryDbContext(options);

        _createdLogger = Substitute.For<ILogger<OrderCreatedEventConsumer>>();
        _cancelledLogger = Substitute.For<ILogger<OrderCancelledEventConsumer>>();
        _confirmedLogger = Substitute.For<ILogger<OrderConfirmedEventConsumer>>();
    }

    [Fact]
    public async Task OrderCreatedEventConsumer_WithValidStock_ShouldReserveStockAndPublishReservedEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var product = new Product { Id = productId, Name = "Regular Product", TotalStock = 20, Version = Array.Empty<byte>() };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        var message = new OrderCreatedEvent(
            OrderId: orderId,
            CustomerId: Guid.NewGuid(),
            TotalAmount: 50.00m,
            Items: new List<OrderItemDto> { new(productId, 5, 10.00m) }, // 5 out of 20 = 25% (allowed)
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        var context = Substitute.For<ConsumeContext<OrderCreatedEvent>>();
        context.Message.Returns(message);

        var consumer = new OrderCreatedEventConsumer(_dbContext, _createdLogger);

        // Act
        await consumer.Consume(context);

        // Assert
        var updatedProduct = await _dbContext.Products.FindAsync(productId);
        Assert.Equal(15, updatedProduct!.TotalStock);

        var reservation = await _dbContext.StockReservations.FirstOrDefaultAsync(r => r.OrderId == orderId);
        Assert.NotNull(reservation);
        Assert.Equal(5, reservation.Quantity);

        await context.Received(1).Publish(
            Arg.Is<StockReservedEvent>(e => e.OrderId == orderId && e.TotalAmount == 50.00m),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task OrderCreatedEventConsumer_WithMoreThan50PercentStockReservation_ShouldFailStockReservation()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var product = new Product { Id = productId, Name = "Regular Product", TotalStock = 20, Version = Array.Empty<byte>() };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        var message = new OrderCreatedEvent(
            OrderId: orderId,
            CustomerId: Guid.NewGuid(),
            TotalAmount: 150.00m,
            Items: new List<OrderItemDto> { new(productId, 15, 10.00m) }, // 15 out of 20 = 75% (violates 50% rule)
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        var context = Substitute.For<ConsumeContext<OrderCreatedEvent>>();
        context.Message.Returns(message);

        var consumer = new OrderCreatedEventConsumer(_dbContext, _createdLogger);

        // Act
        await consumer.Consume(context);

        // Assert
        var updatedProduct = await _dbContext.Products.FindAsync(productId);
        Assert.Equal(20, updatedProduct!.TotalStock); // Stock not reduced

        var reservation = await _dbContext.StockReservations.FirstOrDefaultAsync(r => r.OrderId == orderId);
        Assert.Null(reservation);

        await context.Received(1).Publish(
            Arg.Is<StockReleasedEvent>(e => e.OrderId == orderId && e.Reason.Contains("Cannot reserve more than 50%")),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task OrderCreatedEventConsumer_FlashSaleExceededQuantity_ShouldFailStockReservation()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        // Product name contains "Flash" to trigger flash sale logic
        var product = new Product { Id = productId, Name = "Flash Sale iPhone", TotalStock = 100, Version = Array.Empty<byte>() };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        var message = new OrderCreatedEvent(
            OrderId: orderId,
            CustomerId: Guid.NewGuid(),
            TotalAmount: 3000.00m,
            Items: new List<OrderItemDto> { new(productId, 3, 1000.00m) }, // 3 violates max 2 per customer for flash sales
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        var context = Substitute.For<ConsumeContext<OrderCreatedEvent>>();
        context.Message.Returns(message);

        var consumer = new OrderCreatedEventConsumer(_dbContext, _createdLogger);

        // Act
        await consumer.Consume(context);

        // Assert
        var updatedProduct = await _dbContext.Products.FindAsync(productId);
        Assert.Equal(100, updatedProduct!.TotalStock);

        await context.Received(1).Publish(
            Arg.Is<StockReleasedEvent>(e => e.OrderId == orderId && e.Reason.Contains("Flash sale limit exceeded")),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task OrderCreatedEventConsumer_ProductNotFound_ShouldPublishStockReleasedEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var message = new OrderCreatedEvent(
            OrderId: orderId,
            CustomerId: Guid.NewGuid(),
            TotalAmount: 10.00m,
            Items: new List<OrderItemDto> { new(Guid.NewGuid(), 1, 10.00m) }, // Random ProductId
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        var context = Substitute.For<ConsumeContext<OrderCreatedEvent>>();
        context.Message.Returns(message);

        var consumer = new OrderCreatedEventConsumer(_dbContext, _createdLogger);

        // Act
        await consumer.Consume(context);

        // Assert
        await context.Received(1).Publish(
            Arg.Is<StockReleasedEvent>(e => e.OrderId == orderId && e.Reason.Contains("not found")),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task OrderCancelledEventConsumer_WithReservations_ShouldRestoreStockAndRemoveReservations()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var product = new Product { Id = productId, Name = "Regular Product", TotalStock = 15, Version = Array.Empty<byte>() };
        var reservation = new StockReservation { OrderId = orderId, ProductId = productId, Quantity = 5, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        _dbContext.Products.Add(product);
        _dbContext.StockReservations.Add(reservation);
        await _dbContext.SaveChangesAsync();

        var message = new OrderCancelledEvent(orderId, "Test Cancellation");
        var context = Substitute.For<ConsumeContext<OrderCancelledEvent>>();
        context.Message.Returns(message);

        var consumer = new OrderCancelledEventConsumer(_dbContext, _cancelledLogger);

        // Act
        await consumer.Consume(context);

        // Assert
        var updatedProduct = await _dbContext.Products.FindAsync(productId);
        Assert.Equal(20, updatedProduct!.TotalStock);

        var anyReservations = await _dbContext.StockReservations.AnyAsync(r => r.OrderId == orderId);
        Assert.False(anyReservations);
    }

    [Fact]
    public async Task OrderConfirmedEventConsumer_WithReservations_ShouldRemoveReservationsWithoutRestoringStock()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var product = new Product { Id = productId, Name = "Regular Product", TotalStock = 15, Version = Array.Empty<byte>() };
        var reservation = new StockReservation { OrderId = orderId, ProductId = productId, Quantity = 5, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        _dbContext.Products.Add(product);
        _dbContext.StockReservations.Add(reservation);
        await _dbContext.SaveChangesAsync();

        var message = new OrderConfirmedEvent(orderId);
        var context = Substitute.For<ConsumeContext<OrderConfirmedEvent>>();
        context.Message.Returns(message);

        var consumer = new OrderConfirmedEventConsumer(_dbContext, _confirmedLogger);

        // Act
        await consumer.Consume(context);

        // Assert
        var updatedProduct = await _dbContext.Products.FindAsync(productId);
        Assert.Equal(15, updatedProduct!.TotalStock); // Stock remains 15 (not restored to 20)

        var anyReservations = await _dbContext.StockReservations.AnyAsync(r => r.OrderId == orderId);
        Assert.False(anyReservations);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
