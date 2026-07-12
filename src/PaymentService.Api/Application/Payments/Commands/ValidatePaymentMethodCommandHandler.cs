using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Shared.Enums;

namespace PaymentService.Api.Application.Payments.Commands;

public class ValidatePaymentMethodCommandHandler : IRequestHandler<ValidatePaymentMethodCommand, bool>
{
    public Task<bool> Handle(ValidatePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        bool isValid = request.Method switch
        {
            PaymentMethod.CreditCard => request.Identifier?.Length == 16,
            PaymentMethod.Wallet => !string.IsNullOrEmpty(request.Identifier),
            PaymentMethod.BankTransfer => request.Identifier?.Length == 26 && request.Identifier.StartsWith("TR"),
            _ => false
        };

        return Task.FromResult(isValid);
    }
}
