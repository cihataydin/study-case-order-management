using System.Collections.Generic;
using MediatR;
using InventoryService.Api.Application.Inventory.Dtos;

namespace InventoryService.Api.Application.Inventory.Commands;

public record BulkUpdateStockCommand(List<StockUpdateItemDto> Items) : IRequest<bool>;
