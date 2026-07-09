using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using InventoryService.Api.Infrastructure.Data;

namespace InventoryService.Api.Application.Inventory.Features;

public record GetProductStockQuery(Guid ProductId) : IRequest<int>;
public class GetProductStockQueryHandler : IRequestHandler<GetProductStockQuery, int>
{
    private readonly InventoryDbContext _dbContext;
    public GetProductStockQueryHandler(InventoryDbContext dbContext) => _dbContext = dbContext;
    public async Task<int> Handle(GetProductStockQuery request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        return product?.TotalStock ?? 0;
    }
}

public record CheckAvailabilityDto(Guid ProductId, int Quantity);
public record CheckAvailabilityQuery(List<CheckAvailabilityDto> Items) : IRequest<bool>;
public class CheckAvailabilityQueryHandler : IRequestHandler<CheckAvailabilityQuery, bool>
{
    private readonly InventoryDbContext _dbContext;
    public CheckAvailabilityQueryHandler(InventoryDbContext dbContext) => _dbContext = dbContext;
    public async Task<bool> Handle(CheckAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var productIds = request.Items.Select(x => x.ProductId).ToList();
        var products = await _dbContext.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach(var item in request.Items)
        {
            if(!products.TryGetValue(item.ProductId, out var p) || p.TotalStock < item.Quantity)
                return false;
        }
        return true;
    }
}

public record ReserveStockCommand(Guid OrderId, Guid ProductId, int Quantity) : IRequest<bool>;
public class ReserveStockCommandHandler : IRequestHandler<ReserveStockCommand, bool>
{
    private readonly InventoryDbContext _dbContext;
    public ReserveStockCommandHandler(InventoryDbContext dbContext) => _dbContext = dbContext;
    public async Task<bool> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product == null || product.TotalStock < request.Quantity) return false;
        
        product.TotalStock -= request.Quantity;
        _dbContext.StockReservations.Add(new Domain.Entities.StockReservation { OrderId = request.OrderId, ProductId = request.ProductId, Quantity = request.Quantity, ExpiresAt = DateTime.UtcNow.AddMinutes(10) });
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record ReleaseReservationCommand(Guid OrderId) : IRequest<bool>;
public class ReleaseReservationCommandHandler : IRequestHandler<ReleaseReservationCommand, bool>
{
    private readonly InventoryDbContext _dbContext;
    public ReleaseReservationCommandHandler(InventoryDbContext dbContext) => _dbContext = dbContext;
    public async Task<bool> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken)
    {
        var reservations = await _dbContext.StockReservations.Where(r => r.OrderId == request.OrderId).ToListAsync(cancellationToken);
        if (!reservations.Any()) return false;
        
        foreach(var res in reservations)
        {
            var p = await _dbContext.Products.FirstOrDefaultAsync(x => x.Id == res.ProductId, cancellationToken);
            if (p != null) p.TotalStock += res.Quantity;
        }
        _dbContext.StockReservations.RemoveRange(reservations);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record StockUpdateItemDto(Guid ProductId, int QuantityChange);

public record BulkUpdateStockCommand(List<StockUpdateItemDto> Items) : IRequest<bool>;

public class BulkUpdateStockCommandHandler : IRequestHandler<BulkUpdateStockCommand, bool>
{
    private readonly InventoryDbContext _dbContext;
    public BulkUpdateStockCommandHandler(InventoryDbContext dbContext) => _dbContext = dbContext;
    public async Task<bool> Handle(BulkUpdateStockCommand request, CancellationToken cancellationToken)
    {
        var productIds = request.Items.Select(x => x.ProductId).ToList();
        var products = await _dbContext.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(cancellationToken);

        foreach (var updateItem in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == updateItem.ProductId);
            if (product != null)
            {
                product.TotalStock += updateItem.QuantityChange;
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}

