using FluentValidation;

namespace PaymentService.Api.Application.Payments.Commands;

public class ValidatePaymentMethodCommandValidator : AbstractValidator<ValidatePaymentMethodCommand>
{
    public ValidatePaymentMethodCommandValidator()
    {
        RuleFor(x => x.Method).NotEmpty().WithMessage("Payment method is required.");
        RuleFor(x => x.Identifier).NotEmpty().WithMessage("Payment identifier is required.");
    }
}
