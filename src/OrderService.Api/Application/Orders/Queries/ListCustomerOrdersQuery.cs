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

namespace OrderService.Api.Application.Orders.Queries;

public record ListCustomerOrdersQuery(Guid CustomerId) : IRequest<List<OrderDto>>;

public class ListCustomerOrdersQueryHandler : IRequestHandler<ListCustomerOrdersQuery, List<OrderDto>>
{
    private readonly OrderDbContext _dbContext;
    private readonly IMapper _mapper;

    public ListCustomerOrdersQueryHandler(OrderDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<List<OrderDto>> Handle(ListCustomerOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == request.CustomerId)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
            
        return _mapper.Map<List<OrderDto>>(orders);
    }
}
