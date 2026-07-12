using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Infrastructure.Data;
using PaymentService.Api.Application.Payments.Dtos;
using AutoMapper;

namespace PaymentService.Api.Application.Payments.Queries;

public class GetPaymentStatusQueryHandler : IRequestHandler<GetPaymentStatusQuery, PaymentDto?>
{
    private readonly PaymentDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetPaymentStatusQueryHandler(PaymentDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<PaymentDto?> Handle(GetPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);
        return payment != null ? _mapper.Map<PaymentDto>(payment) : null;
    }
}
