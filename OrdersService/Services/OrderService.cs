using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Models;

namespace OrdersService.Services;

public class OrderService : IOrderService
{
    private readonly OrdersDbContext _dbContext;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrdersDbContext dbContext, ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(string userId, decimal amount, string description)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Description = description,
            Status = OrderStatus.NEW,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Orders.Add(order);

        var paymentRequest = new Shared.Messages.OrderPaymentRequest
        {
            OrderId = order.Id,
            UserId = userId,
            Amount = amount,
            Description = description
        };

        var outboxMessage = new TransactionalOutbox
        {
            Id = Guid.NewGuid(),
            MessageType = typeof(Shared.Messages.OrderPaymentRequest).Name,
            Payload = System.Text.Json.JsonSerializer.Serialize(paymentRequest),
            Published = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TransactionalOutbox.Add(outboxMessage);

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Order {OrderId} created", order.Id);

        return order;
    }

    public async Task<List<Order>> GetOrdersAsync(string userId)
    {
        return await _dbContext.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderAsync(Guid orderId)
    {
        return await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task UpdateOrderStatusAsync(Guid orderId, OrderStatus status)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order != null)
        {
            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Order {OrderId} status updated to {Status}", orderId, status);
        }
    }
}
