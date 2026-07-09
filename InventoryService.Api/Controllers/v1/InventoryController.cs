using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Api.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class InventoryController : ControllerBase
{
    [HttpPost("check-availability")]
    public async Task<IActionResult> CheckAvailability([FromBody] object request)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpPost("reserve")]
    public async Task<IActionResult> ReserveStock([FromBody] object request)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpPost("release")]
    public async Task<IActionResult> ReleaseReservation([FromBody] object request)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpGet("products/{productId:guid}/stock")]
    public async Task<IActionResult> GetCurrentStock(Guid productId)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }
}
