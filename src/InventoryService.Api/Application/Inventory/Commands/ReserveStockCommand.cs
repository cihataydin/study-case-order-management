using System;
using System.Collections.Generic;
using MediatR;
using InventoryService.Api.Application.Inventory.Dtos;

namespace InventoryService.Api.Application.Inventory.Commands;

public record ReserveStockCommand(Guid OrderId, List<ReserveStockItemDto> Items) : IRequest<bool>;
