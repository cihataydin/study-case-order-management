using System;

namespace InventoryService.Api.Application.Inventory.Dtos;

public record ProductAvailabilityResultDto(Guid ProductId, int RequestedQuantity, bool IsAvailable, int AvailableQuantity);
