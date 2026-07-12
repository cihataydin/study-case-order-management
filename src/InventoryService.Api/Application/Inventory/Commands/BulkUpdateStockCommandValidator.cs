using FluentValidation;

namespace InventoryService.Api.Application.Inventory.Commands;

public class BulkUpdateStockCommandValidator : AbstractValidator<BulkUpdateStockCommand>
{
    public BulkUpdateStockCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item must be provided for bulk update.");

        RuleForEach(x => x.Items).ChildRules(items =>
        {
            items.RuleFor(i => i.ProductId).NotEmpty().WithMessage("ProductId is required.");
            items.RuleFor(i => i.QuantityChange).NotEqual(0).WithMessage("QuantityChange cannot be zero.");
        });
    }
}
