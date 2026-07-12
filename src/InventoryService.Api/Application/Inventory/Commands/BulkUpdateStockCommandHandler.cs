using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using InventoryService.Api.Infrastructure.Data;

namespace InventoryService.Api.Application.Inventory.Commands;

public class BulkUpdateStockCommandHandler : IRequestHandler<BulkUpdateStockCommand, bool>
{
    private readonly InventoryDbContext _dbContext;
    private readonly IDistributedCache _cache;
    public BulkUpdateStockCommandHandler(InventoryDbContext dbContext, IDistributedCache cache) 
    {
        _dbContext = dbContext;
        _cache = cache;
    }
    public async Task<bool> Handle(BulkUpdateStockCommand request, CancellationToken cancellationToken)
    {
        var productIds = request.Items.Select(x => x.ProductId).ToList();
        var products = await _dbContext.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(cancellationToken);

        foreach (var updateItem in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == updateItem.ProductId);
            if (product != null)
            {
                if (updateItem.QuantityChange >= 0)
                    product.IncreaseStock(updateItem.QuantityChange);
                else
                    product.DecreaseStock(Math.Abs(updateItem.QuantityChange));
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            foreach (var updateItem in request.Items)
            {
                await _cache.RemoveAsync($"stock_{updateItem.ProductId}", cancellationToken);
            }
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
