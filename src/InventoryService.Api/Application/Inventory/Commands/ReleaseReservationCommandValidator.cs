using FluentValidation;

namespace InventoryService.Api.Application.Inventory.Commands;

public class ReleaseReservationCommandValidator : AbstractValidator<ReleaseReservationCommand>
{
    public ReleaseReservationCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("OrderId is required.");
    }
}
