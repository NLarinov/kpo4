using OrdersService.Models;

namespace OrdersService.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(string userId, decimal amount, string description);
    Task<List<Order>> GetOrdersAsync(string userId);
    Task<Order?> GetOrderAsync(Guid orderId);
    Task UpdateOrderStatusAsync(Guid orderId, OrderStatus status);
}
