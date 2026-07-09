using Microsoft.AspNetCore.Mvc;

using MediatR;
using PaymentService.Api.Application.Payments.Features;

namespace PaymentService.Api.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }
    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentCommand request)
    {
        var result = await _mediator.Send(request);
        if (result) return Ok(new { Message = "Payment processed manually." });
        return BadRequest(new { Message = "Payment processing failed." });
    }

    [HttpPost("refund")]
    public async Task<IActionResult> ProcessRefund([FromBody] ProcessRefundCommand request)
    {
        var result = await _mediator.Send(request);
        if (result) return Ok(new { Message = "Refund processed successfully" });
        return BadRequest(new { Message = "Refund could not be processed" });
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<IActionResult> GetPaymentStatus(Guid paymentId)
    {
        var payment = await _mediator.Send(new GetPaymentStatusQuery(paymentId));
        if (payment == null) return NotFound();
        return Ok(payment);
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidatePaymentMethod([FromBody] ValidatePaymentMethodCommand request)
    {
        var isValid = await _mediator.Send(request);
        return Ok(new { IsValid = isValid });
    }
}
