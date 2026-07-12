using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Infrastructure.Data;
using PaymentService.Api.Domain.Enums;

namespace PaymentService.Api.Application.Payments.Commands;

public class ProcessRefundCommandHandler : IRequestHandler<ProcessRefundCommand, bool>
{
    private readonly PaymentDbContext _dbContext;
    public ProcessRefundCommandHandler(PaymentDbContext dbContext) => _dbContext = dbContext;
    public async Task<bool> Handle(ProcessRefundCommand request, CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);
        if (payment == null || payment.Status != PaymentStatus.Success) return false;
        
        payment.Status = PaymentStatus.RefundPending;
        payment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
