using FluentValidation;

namespace PaymentService.Api.Application.Payments.Commands;

public class ProcessRefundCommandValidator : AbstractValidator<ProcessRefundCommand>
{
    public ProcessRefundCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty().WithMessage("PaymentId is required.");
    }
}
