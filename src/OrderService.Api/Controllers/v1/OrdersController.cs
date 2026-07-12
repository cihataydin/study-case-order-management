using Microsoft.AspNetCore.Mvc;

using MediatR;

using OrderService.Api.Application.Orders.Commands;
using OrderService.Api.Application.Orders.Queries;
using Shared.Exceptions;

namespace OrderService.Api.Controllers.v1;

[ApiController]
[Route("api/v1/orders")]
[Tags("orders")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey, [FromBody] CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new ProblemDetails { Title = "Missing Idempotency Key", Detail = "X-Idempotency-Key header is required." });
        }

        var command = new CreateOrderCommand(request.CustomerId, idempotencyKey, request.Items, request.IsVip, request.PaymentMethod);
        var orderId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetOrderDetails), new { orderId = orderId }, new { OrderId = orderId });
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrderDetails(Guid orderId)
    {
        var order = await _mediator.Send(new GetOrderDetailsQuery(orderId));
        if (order == null) return NotFound();
        return Ok(order);
    }

    [HttpPut("{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid orderId)
    {
        var result = await _mediator.Send(new CancelOrderCommand(orderId));
        if (!result) return BadRequest(new ProblemDetails { Title = "Cancellation Failed", Detail = "Order could not be cancelled. It may not exist or is already completed." });
        return NoContent();
    }

    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> ListCustomerOrders(Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _mediator.Send(new ListCustomerOrdersQuery(customerId, pageNumber, pageSize));
        return Ok(result);
    }


    [HttpPost("{orderId:guid}/retry")]
    public async Task<IActionResult> RetryFailedOrder(Guid orderId)
    {
        var result = await _mediator.Send(new RetryOrderCommand(orderId));
        if (result)
            return NoContent();
            
        return BadRequest(new ProblemDetails { Title = "Retry Failed", Detail = $"Order {orderId} is not Failed. Only failed orders can be retried." });
    }

    // Architectural Note: The API specifications in the case document did not define endpoints
    // for SHIPPED and DELIVERED state transitions. However, since the Order Service requirements
    // explicitly mandate managing these states, the /ship and /deliver endpoints have been added 
    // proactively for Admin/Mock purposes to properly simulate and test the complete state machine.

    [HttpPut("{orderId:guid}/ship")]
    public async Task<IActionResult> ShipOrder(Guid orderId)
    {
        var result = await _mediator.Send(new ShipOrderCommand(orderId));
        if (!result) return BadRequest(new ProblemDetails { Title = "Shipping Failed", Detail = "Order could not be shipped. It may not exist." });
        return NoContent();
    }

    [HttpPut("{orderId:guid}/deliver")]
    public async Task<IActionResult> DeliverOrder(Guid orderId)
    {
        var result = await _mediator.Send(new DeliverOrderCommand(orderId));
        if (!result) return BadRequest(new ProblemDetails { Title = "Delivery Failed", Detail = "Order could not be delivered. It may not exist." });
        return NoContent();
    }
}
