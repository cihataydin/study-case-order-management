using FluentValidation;

namespace PaymentService.Api.Application.Payments.Commands;

public class ValidatePaymentMethodCommandValidator : AbstractValidator<ValidatePaymentMethodCommand>
{
    public ValidatePaymentMethodCommandValidator()
    {
        RuleFor(x => x.Method).IsInEnum().WithMessage("Invalid payment method.");
        RuleFor(x => x.Identifier).NotEmpty().WithMessage("Payment identifier is required.");
    }
}
