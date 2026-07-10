using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using InventoryService.Api.Application.Inventory.Features;
using InventoryService.Api.Domain.Entities;
using InventoryService.Api.Infrastructure.Data;
using Xunit;

namespace InventoryService.Api.UnitTests;

public class InventoryFeaturesTests : IDisposable
{
    private readonly InventoryDbContext _dbContext;
    private readonly IDistributedCache _cache;

    public InventoryFeaturesTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new InventoryDbContext(options);
        _cache = Substitute.For<IDistributedCache>();
    }

    [Fact]
    public async Task GetProductStock_WithCacheHit_ShouldReturnCachedValueWithoutDbQuery()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var cacheKey = $"stock_{productId}";
        var cachedValueBytes = Encoding.UTF8.GetBytes("42");

        _cache.GetAsync(cacheKey, Arg.Any<CancellationToken>()).Returns(cachedValueBytes);

        var handler = new GetProductStockQueryHandler(_dbContext, _cache);

        // Act
        var result = await handler.Handle(new GetProductStockQuery(productId), CancellationToken.None);

        // Assert
        Assert.Equal(42, result);
        // DB context shouldn't have been hit, so product table remains empty
        var dbCount = await _dbContext.Products.CountAsync();
        Assert.Equal(0, dbCount);
    }

    [Fact]
    public async Task GetProductStock_WithCacheMiss_ShouldQueryDbAndSetCache()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new Product { Id = productId, Name = "Product 1", TotalStock = 50, Version = Array.Empty<byte>() };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var handler = new GetProductStockQueryHandler(_dbContext, _cache);

        // Act
        var result = await handler.Handle(new GetProductStockQuery(productId), CancellationToken.None);

        // Assert
        Assert.Equal(50, result);
        await _cache.Received(1).SetAsync(
            $"stock_{productId}",
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "50"),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CheckAvailability_WithAllItemsAvailable_ShouldReturnTrue()
    {
        // Arrange
        var p1 = new Product { Id = Guid.NewGuid(), Name = "P1", TotalStock = 10, Version = Array.Empty<byte>() };
        var p2 = new Product { Id = Guid.NewGuid(), Name = "P2", TotalStock = 20, Version = Array.Empty<byte>() };
        _dbContext.Products.AddRange(p1, p2);
        await _dbContext.SaveChangesAsync();

        var query = new CheckAvailabilityQuery(new List<CheckAvailabilityDto>
        {
            new(p1.Id, 5),
            new(p2.Id, 15)
        });

        var handler = new CheckAvailabilityQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CheckAvailability_WithInsufficientStock_ShouldReturnFalse()
    {
        // Arrange
        var p1 = new Product { Id = Guid.NewGuid(), Name = "P1", TotalStock = 10, Version = Array.Empty<byte>() };
        _dbContext.Products.Add(p1);
        await _dbContext.SaveChangesAsync();

        var query = new CheckAvailabilityQuery(new List<CheckAvailabilityDto>
        {
            new(p1.Id, 15) // requires 15, stock has 10
        });

        var handler = new CheckAvailabilityQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReserveStock_WithSufficientStock_ShouldReduceStockAndCreateReservationAndClearCache()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var product = new Product { Id = productId, Name = "P1", TotalStock = 100, Version = Array.Empty<byte>() };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        var command = new ReserveStockCommand(orderId, productId, 30);
        var handler = new ReserveStockCommandHandler(_dbContext, _cache);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedProduct = await _dbContext.Products.FindAsync(productId);
        Assert.Equal(70, updatedProduct!.TotalStock);

        var reservation = await _dbContext.StockReservations.FirstOrDefaultAsync(r => r.OrderId == orderId && r.ProductId == productId);
        Assert.NotNull(reservation);
        Assert.Equal(30, reservation.Quantity);

        await _cache.Received(1).RemoveAsync($"stock_{productId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReleaseReservation_WithExistingReservations_ShouldRestoreStockAndDeleteReservations()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var product = new Product { Id = productId, Name = "P1", TotalStock = 70, Version = Array.Empty<byte>() };
        var reservation = new StockReservation { OrderId = orderId, ProductId = productId, Quantity = 30, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        _dbContext.Products.Add(product);
        _dbContext.StockReservations.Add(reservation);
        await _dbContext.SaveChangesAsync();

        var command = new ReleaseReservationCommand(orderId);
        var handler = new ReleaseReservationCommandHandler(_dbContext, _cache);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedProduct = await _dbContext.Products.FindAsync(productId);
        Assert.Equal(100, updatedProduct!.TotalStock);

        var anyReservations = await _dbContext.StockReservations.AnyAsync(r => r.OrderId == orderId);
        Assert.False(anyReservations);

        await _cache.Received(1).RemoveAsync($"stock_{productId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateStock_WithCorrectChanges_ShouldApplyAllChangesAndClearCache()
    {
        // Arrange
        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var p1 = new Product { Id = p1Id, Name = "P1", TotalStock = 50, Version = Array.Empty<byte>() };
        var p2 = new Product { Id = p2Id, Name = "P2", TotalStock = 80, Version = Array.Empty<byte>() };
        _dbContext.Products.AddRange(p1, p2);
        await _dbContext.SaveChangesAsync();

        var command = new BulkUpdateStockCommand(new List<StockUpdateItemDto>
        {
            new(p1Id, 10),  // 50 -> 60
            new(p2Id, -20)  // 80 -> 60
        });

        var handler = new BulkUpdateStockCommandHandler(_dbContext, _cache);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedP1 = await _dbContext.Products.FindAsync(p1Id);
        var updatedP2 = await _dbContext.Products.FindAsync(p2Id);

        Assert.Equal(60, updatedP1!.TotalStock);
        Assert.Equal(60, updatedP2!.TotalStock);

        await _cache.Received(1).RemoveAsync($"stock_{p1Id}", Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync($"stock_{p2Id}", Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
