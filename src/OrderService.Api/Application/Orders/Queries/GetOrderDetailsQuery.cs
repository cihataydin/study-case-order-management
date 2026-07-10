using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Infrastructure.Data;
using OrderService.Api.Domain.Entities;

namespace OrderService.Api.Application.Orders.Queries;

public record GetOrderDetailsQuery(Guid OrderId) : IRequest<Order?>;

public class GetOrderDetailsQueryHandler : IRequestHandler<GetOrderDetailsQuery, Order?>
{
    private readonly OrderDbContext _dbContext;

    public GetOrderDetailsQueryHandler(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Order?> Handle(GetOrderDetailsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
    }
}
