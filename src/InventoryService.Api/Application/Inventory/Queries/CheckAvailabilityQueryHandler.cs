using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using InventoryService.Api.Infrastructure.Data;
using InventoryService.Api.Application.Inventory.Dtos;

namespace InventoryService.Api.Application.Inventory.Queries;

public class CheckAvailabilityQueryHandler : IRequestHandler<CheckAvailabilityQuery, CheckAvailabilityResponseDto>
{
    private readonly InventoryDbContext _dbContext;
    public CheckAvailabilityQueryHandler(InventoryDbContext dbContext) => _dbContext = dbContext;
    public async Task<CheckAvailabilityResponseDto> Handle(CheckAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var productIds = request.Items.Select(x => x.ProductId).ToList();
        var products = await _dbContext.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var resultItems = new List<ProductAvailabilityResultDto>();
        bool isAllAvailable = true;

        foreach(var item in request.Items)
        {
            int availableQuantity = 0;
            bool isAvailable = false;

            if(products.TryGetValue(item.ProductId, out var p))
            {
                availableQuantity = p.TotalStock;
                isAvailable = p.TotalStock >= item.Quantity;
            }

            if (!isAvailable)
            {
                isAllAvailable = false;
            }

            resultItems.Add(new ProductAvailabilityResultDto(
                item.ProductId, 
                item.Quantity, 
                isAvailable, 
                availableQuantity));
        }
        
        return new CheckAvailabilityResponseDto(isAllAvailable, resultItems);
    }
}
