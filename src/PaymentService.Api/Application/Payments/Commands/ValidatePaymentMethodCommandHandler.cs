using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PaymentService.Api.Application.Payments.Commands;

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
