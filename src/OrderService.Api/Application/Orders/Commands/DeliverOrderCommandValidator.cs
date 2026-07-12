using FluentValidation;

namespace OrderService.Api.Application.Orders.Commands;

public class DeliverOrderCommandValidator : AbstractValidator<DeliverOrderCommand>
{
    public DeliverOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("OrderId is required.");
    }
}
