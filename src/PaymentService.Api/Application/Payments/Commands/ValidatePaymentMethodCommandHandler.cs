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
            PaymentMethod.CreditCard => request.Identifier?.Length == 16, // Mock: Card must be 16 digits
            PaymentMethod.Wallet => !string.IsNullOrEmpty(request.Identifier), // Mock: Just checking if wallet ID exists
            PaymentMethod.BankTransfer => request.Identifier?.Length == 26 && request.Identifier.StartsWith("TR"), // Mock: Simple IBAN validation
            _ => false
        };

        return Task.FromResult(isValid);
    }
}
