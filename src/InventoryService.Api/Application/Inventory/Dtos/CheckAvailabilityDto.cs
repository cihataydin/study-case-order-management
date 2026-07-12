using System;

namespace InventoryService.Api.Application.Inventory.Dtos;

public record CheckAvailabilityDto(Guid ProductId, int Quantity);
