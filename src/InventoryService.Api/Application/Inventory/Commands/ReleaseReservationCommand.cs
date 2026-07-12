using System;
using MediatR;

namespace InventoryService.Api.Application.Inventory.Commands;

public record ReleaseReservationCommand(Guid OrderId) : IRequest<bool>;
