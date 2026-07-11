using Grpc.Core;
using Shared.Grpc;
using Microsoft.EntityFrameworkCore;
using InventoryService.Api.Infrastructure.Data;

namespace InventoryService.Api.Application.Grpc;

public class InventoryGrpcServiceImpl : Shared.Grpc.InventoryGrpcService.InventoryGrpcServiceBase
{
    private readonly InventoryDbContext _dbContext;

    public InventoryGrpcServiceImpl(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<GetProductPricesResponse> GetProductPrices(
        GetProductPricesRequest request, 
        ServerCallContext context)
    {
        var productIds = request.ProductIds.Select(Guid.Parse).ToList();

        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Price })
            .ToListAsync(context.CancellationToken);

        var response = new GetProductPricesResponse();
        
        foreach (var product in products)
        {
            response.Products.Add(new ProductPriceItem
            {
                ProductId = product.Id.ToString(),
                Price = (double)product.Price
            });
        }

        return response;
    }
}
