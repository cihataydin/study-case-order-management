using System;

namespace InventoryService.Api.Application.Inventory.Dtos;

public record ReserveStockItemDto(Guid ProductId, int Quantity);
