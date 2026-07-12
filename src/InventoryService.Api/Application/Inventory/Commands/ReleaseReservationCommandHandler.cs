using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using InventoryService.Api.Infrastructure.Data;

namespace InventoryService.Api.Application.Inventory.Commands;

public class ReleaseReservationCommandHandler : IRequestHandler<ReleaseReservationCommand, bool>
{
    private readonly InventoryDbContext _dbContext;
    private readonly IDistributedCache _cache;
    public ReleaseReservationCommandHandler(InventoryDbContext dbContext, IDistributedCache cache) 
    {
        _dbContext = dbContext;
        _cache = cache;
    }
    public async Task<bool> Handle(ReleaseReservationCommand request, CancellationToken cancellationToken)
    {
        var reservations = await _dbContext.StockReservations.Where(r => r.OrderId == request.OrderId).ToListAsync(cancellationToken);
        if (!reservations.Any()) return false;
        
        var productIds = reservations.Select(r => r.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach(var res in reservations)
        {
            if (products.TryGetValue(res.ProductId, out var p))
            {
                p.IncreaseStock(res.Quantity);
            }
        }
        
        _dbContext.StockReservations.RemoveRange(reservations);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        foreach(var res in reservations)
        {
            await _cache.RemoveAsync($"stock_{res.ProductId}", cancellationToken);
        }
        
        return true;
    }
}
