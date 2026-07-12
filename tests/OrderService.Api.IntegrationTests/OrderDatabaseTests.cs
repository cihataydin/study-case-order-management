using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Domain.Entities;
using OrderService.Api.Domain.Enums;
using OrderService.Api.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderService.Api.IntegrationTests;

public class OrderDatabaseTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
    }

    [Fact]
    public async Task Database_MigrationAndCrudOperations_ShouldSucceedOnRealPostgres()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        // Act: Apply migrations
        using (var context = new OrderDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        // Act: Insert an order
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            CustomerId = customerId,
            IdempotencyKey = "unique-integration-key-123",
            TotalAmount = 250.00m,
            Items = new List<OrderItem>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 125.00m }
            }
        }.WithStatus(OrderStatus.Pending);

        using (var context = new OrderDbContext(options))
        {
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        // Assert: Read and verify order from DB
        using (var context = new OrderDbContext(options))
        {
            var savedOrder = await context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            Assert.NotNull(savedOrder);
            Assert.Equal(customerId, savedOrder.CustomerId);
            Assert.Equal("unique-integration-key-123", savedOrder.IdempotencyKey);
            Assert.Equal(250.00m, savedOrder.TotalAmount);
            Assert.Equal(OrderStatus.Pending, savedOrder.Status);
            Assert.Single(savedOrder.Items);
            Assert.Equal(125.00m, savedOrder.Items.First().UnitPrice);
        }
    }

    [Fact]
    public async Task Database_DuplicateIdempotencyKey_ShouldThrowUniqueConstraintViolationException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        // Apply migrations
        using (var context = new OrderDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        var idempotencyKey = "duplicate-key";

        var order1 = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            TotalAmount = 100.00m
        }.WithStatus(OrderStatus.Pending);

        var order2 = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey, // Duplicate Key
            TotalAmount = 200.00m
        }.WithStatus(OrderStatus.Pending);

        // Act & Assert
        using (var context = new OrderDbContext(options))
        {
            context.Orders.Add(order1);
            await context.SaveChangesAsync();
        }

        using (var context = new OrderDbContext(options))
        {
            context.Orders.Add(order2);
            // Should throw DbUpdateException due to unique constraint index violation on IdempotencyKey
            await Assert.ThrowsAsync<DbUpdateException>(async () => 
                await context.SaveChangesAsync()
            );
        }
    }
}
