using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Infrastructure.Data;
using PaymentService.Api.Domain.Entities;
using PaymentService.Api.Domain.Enums;

namespace PaymentService.Api.Application.Payments.Features;

public record GetPaymentStatusQuery(Guid PaymentId) : IRequest<Payment?>;
public class GetPaymentStatusQueryHandler : IRequestHandler<GetPaymentStatusQuery, Payment?>
{
    private readonly PaymentDbContext _dbContext;
    public GetPaymentStatusQueryHandler(PaymentDbContext dbContext) => _dbContext = dbContext;
    public async Task<Payment?> Handle(GetPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);
    }
}

public record ProcessRefundCommand(Guid PaymentId) : IRequest<bool>;
public class ProcessRefundCommandHandler : IRequestHandler<ProcessRefundCommand, bool>
{
    private readonly PaymentDbContext _dbContext;
    public ProcessRefundCommandHandler(PaymentDbContext dbContext) => _dbContext = dbContext;
    public async Task<bool> Handle(ProcessRefundCommand request, CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);
        if (payment == null || payment.Status != PaymentStatus.Success) return false;
        
        // Removed Task.Delay(1000) simulation.
        // In a real system, the refund takes 3-5 days. We set the status to RefundPending,
        // and a BackgroundWorker (RefundProcessingWorker) will process it asynchronously later.
        payment.Status = PaymentStatus.RefundPending;
        payment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record ValidatePaymentMethodCommand(string Method, string CardNumber) : IRequest<bool>;
public class ValidatePaymentMethodCommandHandler : IRequestHandler<ValidatePaymentMethodCommand, bool>
{
    public Task<bool> Handle(ValidatePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        // Simple mock rule: CardNumber must be 16 digits
        return Task.FromResult(request.CardNumber?.Length == 16);
    }
}

public record ProcessPaymentCommand(Guid OrderId, decimal Amount, string Method) : IRequest<bool>;
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
