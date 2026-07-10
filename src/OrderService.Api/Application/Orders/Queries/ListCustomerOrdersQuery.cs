using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Infrastructure.Data;
using OrderService.Api.Domain.Entities;

namespace OrderService.Api.Application.Orders.Queries;

public record ListCustomerOrdersQuery(Guid CustomerId) : IRequest<List<Order>>;

public class ListCustomerOrdersQueryHandler : IRequestHandler<ListCustomerOrdersQuery, List<Order>>
{
    private readonly OrderDbContext _dbContext;

    public ListCustomerOrdersQueryHandler(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Order>> Handle(ListCustomerOrdersQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == request.CustomerId)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
