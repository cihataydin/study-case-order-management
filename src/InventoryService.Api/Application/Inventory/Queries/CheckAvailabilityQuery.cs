using System.Collections.Generic;
using MediatR;
using InventoryService.Api.Application.Inventory.Dtos;

namespace InventoryService.Api.Application.Inventory.Queries;

public record CheckAvailabilityQuery(List<CheckAvailabilityDto> Items) : IRequest<CheckAvailabilityResponseDto>;
