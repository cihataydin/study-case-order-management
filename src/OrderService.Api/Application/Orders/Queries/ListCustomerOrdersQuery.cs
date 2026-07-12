using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Infrastructure.Data;
using OrderService.Api.Application.Orders.Dtos;
using AutoMapper;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace OrderService.Api.Application.Orders.Queries;

public record PagedResult<T>(List<T> Items, int TotalCount, int PageNumber, int PageSize);

public record ListCustomerOrdersQuery(Guid CustomerId, int PageNumber = 1, int PageSize = 10) : IRequest<PagedResult<OrderDto>>;

public class ListCustomerOrdersQueryHandler : IRequestHandler<ListCustomerOrdersQuery, PagedResult<OrderDto>>
{
    private readonly OrderDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _cache;

    public ListCustomerOrdersQueryHandler(OrderDbContext dbContext, IMapper mapper, IDistributedCache cache)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<PagedResult<OrderDto>> Handle(ListCustomerOrdersQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"customer_orders_{request.CustomerId}_page_{request.PageNumber}_size_{request.PageSize}";
        
        var cachedOrders = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedOrders))
        {
            var cachedResult = JsonSerializer.Deserialize<PagedResult<OrderDto>>(cachedOrders);
            if (cachedResult != null)
            {
                return cachedResult;
            }
        }

        var query = _dbContext.Orders
            .Where(o => o.CustomerId == request.CustomerId);
            
        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
            
        var items = _mapper.Map<List<OrderDto>>(orders);
        var result = new PagedResult<OrderDto>(items, totalCount, request.PageNumber, request.PageSize);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        }, cancellationToken);

        return result;
    }
}
