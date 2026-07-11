using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Infrastructure.Data;
using OrderService.Api.Application.Orders.Dtos;
using AutoMapper;

namespace OrderService.Api.Application.Orders.Queries;

public record GetOrderDetailsQuery(Guid OrderId) : IRequest<OrderDto?>;

public class GetOrderDetailsQueryHandler : IRequestHandler<GetOrderDetailsQuery, OrderDto?>
{
    private readonly OrderDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetOrderDetailsQueryHandler(OrderDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<OrderDto?> Handle(GetOrderDetailsQuery request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
            
        return order == null ? null : _mapper.Map<OrderDto>(order);
    }
}
