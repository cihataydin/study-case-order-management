using Microsoft.AspNetCore.Mvc;

namespace OrderService.Api.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] object request)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrderDetails(Guid orderId)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpPut("{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid orderId)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> ListCustomerOrders(Guid customerId)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpPost("{orderId:guid}/retry")]
    public async Task<IActionResult> RetryFailedOrder(Guid orderId)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }
}
