using Microsoft.AspNetCore.Mvc;

using MediatR;
using InventoryService.Api.Application.Inventory.Commands;
using InventoryService.Api.Application.Inventory.Queries;
using InventoryService.Api.Application.Inventory.Dtos;

namespace InventoryService.Api.Controllers.v1;

[ApiController]
[Route("api/v1/inventory")]
[Tags("inventory")]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }
    [HttpPost("check-availability")]
    public async Task<IActionResult> CheckAvailability([FromBody] CheckAvailabilityQuery request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }

    [HttpPost("reserve")]
    public async Task<IActionResult> ReserveStock([FromBody] ReserveStockCommand request)
    {
        var result = await _mediator.Send(request);
        if (result) return NoContent();
        return BadRequest(new ProblemDetails { Title = "Reservation Failed", Detail = "Could not reserve stock." });
    }

    [HttpPost("release")]
    public async Task<IActionResult> ReleaseReservation([FromBody] ReleaseReservationCommand request)
    {
        var result = await _mediator.Send(request);
        if (result) return NoContent();
        return BadRequest(new ProblemDetails { Title = "Release Failed", Detail = "Could not release reservation. It may not exist." });
    }


    [HttpGet("products/{productId:guid}/stock")]
    public async Task<IActionResult> GetCurrentStock(Guid productId)
    {
        var stock = await _mediator.Send(new GetProductStockQuery(productId));
        return Ok(new { ProductId = productId, AvailableStock = stock });
    }

    [HttpPut("bulk-update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkUpdateStock([FromBody] BulkUpdateStockCommand command)
    {
        var isSuccess = await _mediator.Send(command);

        if (!isSuccess)
        {
            return BadRequest(new ProblemDetails { Title = "Update Failed", Detail = "Stok güncellemesi sırasında bir çakışma (Concurrency) oluştu veya işlem başarısız oldu." });
        }

        return NoContent();
    }
}
