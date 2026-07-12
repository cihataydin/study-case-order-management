using FluentValidation;
using System.Linq;

namespace OrderService.Api.Application.Orders.Commands;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("Order must contain at least one item.");
        
        RuleFor(x => x.Items)
            .Must(items => items != null && items.Count <= 20)
            .WithMessage("Maximum items per order is 20.");



        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .Must(m => new[] { "CreditCard", "Wallet", "BankTransfer" }.Contains(m))
            .WithMessage("Payment method must be CreditCard, Wallet, or BankTransfer.");

        RuleForEach(x => x.Items).ChildRules(items =>
        {
            items.RuleFor(i => i.ProductId).NotEmpty();
            items.RuleFor(i => i.Quantity).GreaterThan(0);
            // items.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
