using Microsoft.AspNetCore.Mvc;
using OrdersService.Models;
using OrdersService.Services;

namespace OrdersService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromBody] CreateOrderRequest request)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID is required");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero");
        }

        var order = await _orderService.CreateOrderAsync(userId, request.Amount, request.Description ?? string.Empty);
        return Ok(order);
    }

    [HttpGet]
    public async Task<ActionResult<List<Order>>> GetOrders([FromHeader(Name = "X-User-Id")] string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID is required");
        }

        var orders = await _orderService.GetOrdersAsync(userId);
        return Ok(orders);
    }

    [HttpGet("{orderId}")]
    public async Task<ActionResult<Order>> GetOrder(Guid orderId)
    {
        var order = await _orderService.GetOrderAsync(orderId);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }
}

public class CreateOrderRequest
{
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
