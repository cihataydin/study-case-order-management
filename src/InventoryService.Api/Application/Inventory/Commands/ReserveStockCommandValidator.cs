using FluentValidation;

namespace InventoryService.Api.Application.Inventory.Commands;

public class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("OrderId is required.");
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item must be provided.")
            .Must(items => items != null && items.Count <= 20).WithMessage("Maximum 20 items allowed per reservation.");

        RuleForEach(x => x.Items).ChildRules(items =>
        {
            items.RuleFor(i => i.ProductId).NotEmpty().WithMessage("ProductId is required.");
            items.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
        });
    }
}
