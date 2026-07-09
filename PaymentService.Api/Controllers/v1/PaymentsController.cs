using Microsoft.AspNetCore.Mvc;

namespace PaymentService.Api.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class PaymentsController : ControllerBase
{
    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment([FromBody] object request)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpPost("refund")]
    public async Task<IActionResult> ProcessRefund([FromBody] object request)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<IActionResult> GetPaymentStatus(Guid paymentId)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidatePaymentMethod([FromBody] object request)
    {
        return await Task.FromResult(StatusCode(StatusCodes.Status501NotImplemented, "Not Implemented Yet"));
    }
}
