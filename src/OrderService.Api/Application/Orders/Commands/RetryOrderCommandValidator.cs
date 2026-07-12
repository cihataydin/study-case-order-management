using FluentValidation;

namespace OrderService.Api.Application.Orders.Commands;

public class RetryOrderCommandValidator : AbstractValidator<RetryOrderCommand>
{
    public RetryOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("OrderId is required.");
    }
}
