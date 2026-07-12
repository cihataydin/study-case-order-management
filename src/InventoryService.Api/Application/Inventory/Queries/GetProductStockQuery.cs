using System;
using MediatR;

namespace InventoryService.Api.Application.Inventory.Queries;

public record GetProductStockQuery(Guid ProductId) : IRequest<int>;
