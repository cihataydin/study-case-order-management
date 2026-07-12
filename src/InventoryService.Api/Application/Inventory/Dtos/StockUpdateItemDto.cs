using System;

namespace InventoryService.Api.Application.Inventory.Dtos;

public record StockUpdateItemDto(Guid ProductId, int QuantityChange);
