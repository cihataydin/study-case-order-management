using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using InventoryService.Api.Infrastructure.Data;

namespace InventoryService.Api.Application.Inventory.Commands;

public class ReserveStockCommandHandler : IRequestHandler<ReserveStockCommand, bool>
{
    private readonly InventoryDbContext _dbContext;
    private readonly IDistributedCache _cache;
    public ReserveStockCommandHandler(InventoryDbContext dbContext, IDistributedCache cache) 
    {
        _dbContext = dbContext;
        _cache = cache;
    }
    public async Task<bool> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        var alreadyReserved = await _dbContext.StockReservations.AnyAsync(r => r.OrderId == request.OrderId, cancellationToken);
        if (alreadyReserved)
        {
            return true;
        }

        var productIds = request.Items.Select(x => x.ProductId).ToList();
        var products = await _dbContext.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        
        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || product.TotalStock < item.Quantity) return false;
            
            try
            {
                product.DecreaseStock(item.Quantity, applyReservationLimit: true);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            _dbContext.StockReservations.Add(new Domain.Entities.StockReservation { OrderId = request.OrderId, ProductId = item.ProductId, Quantity = item.Quantity, ExpiresAt = System.DateTime.UtcNow.AddMinutes(10) });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        foreach (var item in request.Items)
        {
            await _cache.RemoveAsync($"stock_{item.ProductId}", cancellationToken);
        }
        
        return true;
    }
}
