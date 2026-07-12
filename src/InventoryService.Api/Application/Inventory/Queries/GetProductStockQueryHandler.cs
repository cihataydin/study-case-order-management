using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using InventoryService.Api.Infrastructure.Data;

namespace InventoryService.Api.Application.Inventory.Queries;

public class GetProductStockQueryHandler : IRequestHandler<GetProductStockQuery, int>
{
    private readonly InventoryDbContext _dbContext;
    private readonly IDistributedCache _cache;
    public GetProductStockQueryHandler(InventoryDbContext dbContext, IDistributedCache cache) 
    {
        _dbContext = dbContext;
        _cache = cache;
    }
    public async Task<int> Handle(GetProductStockQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"stock_{request.ProductId}";
        var cachedStock = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedStock) && int.TryParse(cachedStock, out int stock))
        {
            return stock;
        }

        var product = await _dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        var actualStock = product?.TotalStock ?? 0;
        
        await _cache.SetStringAsync(cacheKey, actualStock.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        }, cancellationToken);

        return actualStock;
    }
}
