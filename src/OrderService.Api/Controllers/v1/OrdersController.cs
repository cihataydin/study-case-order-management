using Microsoft.AspNetCore.Mvc;

using MediatR;

using OrderService.Api.Application.Orders.Commands;
using OrderService.Api.Application.Orders.Queries;

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
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand request)
    {
        var orderId = await _mediator.Send(request);
        return Ok(new { OrderId = orderId });
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
        try 
        {
            var result = await _mediator.Send(new CancelOrderCommand(orderId));
            if (!result) return BadRequest(new { Message = "Order could not be cancelled. It may not exist or is already completed." });
            return Ok(new { Message = "Order cancelled successfully." });
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> ListCustomerOrders(Guid customerId)
    {
        var orders = await _mediator.Send(new ListCustomerOrdersQuery(customerId));
        return Ok(orders);
    }


    [HttpPost("{orderId:guid}/retry")]
    public async Task<IActionResult> RetryFailedOrder(Guid orderId)
    {
        var result = await _mediator.Send(new RetryOrderCommand(orderId));
        if (result)
            return Ok(new { Message = "Order retry initiated." });
            
        return BadRequest(new { Message = $"Order {orderId} is not Failed. Only failed orders can be retried." });
    }

    // Architectural Note: The API specifications in the case document did not define endpoints
    // for SHIPPED and DELIVERED state transitions. However, since the Order Service requirements
    // explicitly mandate managing these states, the /ship and /deliver endpoints have been added 
    // proactively for Admin/Mock purposes to properly simulate and test the complete state machine.

    [HttpPut("{orderId:guid}/ship")]
    public async Task<IActionResult> ShipOrder(Guid orderId)
    {
        try 
        {
            var result = await _mediator.Send(new ShipOrderCommand(orderId));
            if (!result) return BadRequest(new { Message = "Order could not be shipped. It may not exist." });
            return Ok(new { Message = "Order shipped successfully." });
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPut("{orderId:guid}/deliver")]
    public async Task<IActionResult> DeliverOrder(Guid orderId)
    {
        try 
        {
            var result = await _mediator.Send(new DeliverOrderCommand(orderId));
            if (!result) return BadRequest(new { Message = "Order could not be delivered. It may not exist." });
            return Ok(new { Message = "Order delivered successfully." });
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
