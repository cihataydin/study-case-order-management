using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Infrastructure.Data;
using PaymentService.Api.Domain.Entities;
using PaymentService.Api.Domain.Enums;

using AutoMapper;

namespace PaymentService.Api.Application.Payments.Features;

public record PaymentDto(Guid Id, Guid OrderId, decimal Amount, PaymentStatus Status, string Method, DateTime CreatedAt, DateTime? UpdatedAt);

public record GetPaymentStatusQuery(Guid PaymentId) : IRequest<PaymentDto?>;
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

public record ValidatePaymentMethodCommand(string Method, string Identifier) : IRequest<bool>;
public class ValidatePaymentMethodCommandHandler : IRequestHandler<ValidatePaymentMethodCommand, bool>
{
    public Task<bool> Handle(ValidatePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        // Case Study Requirement: "Multiple payment methods support (Credit Card, Wallet, Bank Transfer)"
        bool isValid = request.Method switch
        {
            "CreditCard" => request.Identifier?.Length == 16, // Mock: Card must be 16 digits
            "Wallet" => !string.IsNullOrEmpty(request.Identifier), // Mock: Just checking if wallet ID exists
            "BankTransfer" => request.Identifier?.Length == 26 && request.Identifier.StartsWith("TR"), // Mock: Simple IBAN validation
            _ => false
        };

        return Task.FromResult(isValid);
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
