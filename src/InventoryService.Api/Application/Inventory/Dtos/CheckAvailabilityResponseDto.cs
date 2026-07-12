using System.Collections.Generic;

namespace InventoryService.Api.Application.Inventory.Dtos;

public record CheckAvailabilityResponseDto(bool IsAllAvailable, List<ProductAvailabilityResultDto> Items);
