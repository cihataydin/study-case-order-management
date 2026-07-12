using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace InventoryService.Api.Application.Services;

public class FlashSaleService : IFlashSaleService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<FlashSaleService> _logger;

    public FlashSaleService(IConnectionMultiplexer redis, ILogger<FlashSaleService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<(bool IsAllowed, string ErrorMessage)> CheckFlashSaleLimitAsync(Guid customerId, Guid productId, int quantity, string productName)
    {
        bool isFlashSale = productName.Contains("Flash");
        if (!isFlashSale)
            return (true, string.Empty);

        var cacheKey = $"Inventory_FlashSale_{customerId}_{productId}";
        try
        {
            var db = _redis.GetDatabase();
            long newCount = await db.StringIncrementAsync(cacheKey, quantity);
            
            if (newCount == quantity) 
            {
                await db.KeyExpireAsync(cacheKey, TimeSpan.FromDays(30));
            }

            if (newCount > 2)
            {
                await db.StringDecrementAsync(cacheKey, quantity);
                return (false, $"Flash sale limit exceeded for product {productId}");
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Redis cache for Flash Sale validation. Rejecting order to prevent overselling. CustomerId: {CustomerId}, ProductId: {ProductId}", customerId, productId);
            return (false, "System error during Flash Sale validation.");
        }
    }
}
