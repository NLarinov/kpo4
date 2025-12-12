using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrdersService.Data;
using OrdersService.Models;
using OrdersService.Services;

namespace OrdersService.Tests;

public class OrderServiceTests
{
    private OrdersDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new OrdersDbContext(options);
    }

    [Fact]
    public async Task CreateOrder_ShouldCreateOrderWithNewStatus()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<OrderService>>();
        var service = new OrderService(dbContext, logger.Object);

        var order = await service.CreateOrderAsync("user-1", 100, "Test order");

        Assert.NotNull(order);
        Assert.Equal("user-1", order.UserId);
        Assert.Equal(100, order.Amount);
        Assert.Equal("Test order", order.Description);
        Assert.Equal(OrderStatus.NEW, order.Status);

        var outboxMessages = await dbContext.TransactionalOutbox.ToListAsync();
        Assert.Single(outboxMessages);
    }

    [Fact]
    public async Task GetOrders_ShouldReturnUserOrders()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<OrderService>>();
        var service = new OrderService(dbContext, logger.Object);

        await service.CreateOrderAsync("user-1", 100, "Order 1");
        await service.CreateOrderAsync("user-1", 200, "Order 2");
        await service.CreateOrderAsync("user-2", 300, "Order 3");

        var orders = await service.GetOrdersAsync("user-1");

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal("user-1", o.UserId));
    }

    [Fact]
    public async Task GetOrder_ShouldReturnOrderById()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<OrderService>>();
        var service = new OrderService(dbContext, logger.Object);

        var order = await service.CreateOrderAsync("user-1", 100, "Test order");

        var retrievedOrder = await service.GetOrderAsync(order.Id);

        Assert.NotNull(retrievedOrder);
        Assert.Equal(order.Id, retrievedOrder.Id);
        Assert.Equal("user-1", retrievedOrder.UserId);
    }

    [Fact]
    public async Task UpdateOrderStatus_ShouldChangeStatus()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<OrderService>>();
        var service = new OrderService(dbContext, logger.Object);

        var order = await service.CreateOrderAsync("user-1", 100, "Test order");
        Assert.Equal(OrderStatus.NEW, order.Status);

        await service.UpdateOrderStatusAsync(order.Id, OrderStatus.FINISHED);

        var updatedOrder = await service.GetOrderAsync(order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.FINISHED, updatedOrder.Status);
    }
}
