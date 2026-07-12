using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PaymentService.Api.Infrastructure.Data;
using PaymentService.Api.Domain.Entities;
using PaymentService.Api.Domain.Enums;

namespace PaymentService.Api.Application.Payments.Commands;

public class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, bool>
{
    private readonly PaymentDbContext _dbContext;
    public ProcessPaymentCommandHandler(PaymentDbContext dbContext) => _dbContext = dbContext;
    public async Task<bool> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = new Payment { OrderId = request.OrderId, Amount = request.Amount, Method = request.Method, Status = PaymentStatus.Success };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
