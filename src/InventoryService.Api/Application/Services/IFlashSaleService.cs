using System;
using System.Threading.Tasks;

namespace InventoryService.Api.Application.Services;

public interface IFlashSaleService
{
    Task<(bool IsAllowed, string ErrorMessage)> CheckFlashSaleLimitAsync(Guid customerId, Guid productId, int quantity, string productName);
}
