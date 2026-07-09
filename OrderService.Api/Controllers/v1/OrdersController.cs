using Microsoft.AspNetCore.Mvc;

using MediatR;

namespace OrderService.Api.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Application.Orders.Commands.CreateOrderCommand request)
    {
        var orderId = await _mediator.Send(request);
        return Ok(new { OrderId = orderId });
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrderDetails(Guid orderId)
    {
        var order = await _mediator.Send(new Application.Orders.Queries.GetOrderDetailsQuery(orderId));
        if (order == null) return NotFound();
        return Ok(order);
    }

    [HttpPut("{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid orderId)
    {
        try 
        {
            var result = await _mediator.Send(new Application.Orders.Commands.CancelOrderCommand(orderId));
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
        var orders = await _mediator.Send(new Application.Orders.Queries.ListCustomerOrdersQuery(customerId));
        return Ok(orders);
    }


    [HttpPost("{orderId:guid}/retry")]
    public async Task<IActionResult> RetryFailedOrder(Guid orderId)
    {
        var result = await _mediator.Send(new Application.Orders.Commands.RetryOrderCommand(orderId));
        if (result)
            return Ok(new { Message = "Order retry initiated." });
            
        return BadRequest(new { Message = "Could not retry order. It must exist and be in Cancelled status." });
    }


}
