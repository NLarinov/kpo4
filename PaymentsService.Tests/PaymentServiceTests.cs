using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PaymentsService.Data;
using PaymentsService.Models;
using PaymentsService.Services;
using Shared.Messages;

namespace PaymentsService.Tests;

public class PaymentServiceTests
{
    private PaymentsDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PaymentsDbContext(options);
    }

    [Fact]
    public async Task CreateAccount_ShouldCreateNewAccount()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<PaymentService>>();
        var messagePublisher = new Mock<IMessagePublisher>();
        var service = new PaymentService(dbContext, messagePublisher.Object, logger.Object);

        var account = await service.CreateAccountAsync("user-1");

        Assert.NotNull(account);
        Assert.Equal("user-1", account.UserId);
        Assert.Equal(0, account.Balance);
    }

    [Fact]
    public async Task CreateAccount_ShouldReturnNull_WhenAccountExists()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<PaymentService>>();
        var messagePublisher = new Mock<IMessagePublisher>();
        var service = new PaymentService(dbContext, messagePublisher.Object, logger.Object);

        await service.CreateAccountAsync("user-1");

        var account = await service.CreateAccountAsync("user-1");

        Assert.Null(account);
    }

    [Fact]
    public async Task TopUpAccount_ShouldIncreaseBalance()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<PaymentService>>();
        var messagePublisher = new Mock<IMessagePublisher>();
        var service = new PaymentService(dbContext, messagePublisher.Object, logger.Object);

        await service.CreateAccountAsync("user-1");

        var account = await service.TopUpAccountAsync("user-1", 100);

        Assert.NotNull(account);
        Assert.Equal(100, account.Balance);
    }

    [Fact]
    public async Task ProcessPayment_ShouldDeductAmount_WhenSufficientFunds()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<PaymentService>>();
        var messagePublisher = new Mock<IMessagePublisher>();
        var service = new PaymentService(dbContext, messagePublisher.Object, logger.Object);

        await service.CreateAccountAsync("user-1");
        await service.TopUpAccountAsync("user-1", 100);

        var request = new OrderPaymentRequest
        {
            OrderId = Guid.NewGuid(),
            UserId = "user-1",
            Amount = 50,
            Description = "Test order"
        };

        var inboxMessage = new TransactionalInbox
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid().ToString(),
            MessageType = typeof(OrderPaymentRequest).Name,
            Payload = System.Text.Json.JsonSerializer.Serialize(request),
            Processed = false,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.TransactionalInbox.Add(inboxMessage);
        await dbContext.SaveChangesAsync();

        await service.ProcessPaymentAsync(inboxMessage);

        var account = await service.GetAccountAsync("user-1");
        Assert.NotNull(account);
        Assert.Equal(50, account.Balance);

        var transaction = await dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.OrderId == request.OrderId);
        Assert.NotNull(transaction);
        Assert.True(transaction.Success);
    }

    [Fact]
    public async Task ProcessPayment_ShouldFail_WhenInsufficientFunds()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<PaymentService>>();
        var messagePublisher = new Mock<IMessagePublisher>();
        var service = new PaymentService(dbContext, messagePublisher.Object, logger.Object);

        await service.CreateAccountAsync("user-1");
        await service.TopUpAccountAsync("user-1", 50);

        var request = new OrderPaymentRequest
        {
            OrderId = Guid.NewGuid(),
            UserId = "user-1",
            Amount = 100,
            Description = "Test order"
        };

        var inboxMessage = new TransactionalInbox
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid().ToString(),
            MessageType = typeof(OrderPaymentRequest).Name,
            Payload = System.Text.Json.JsonSerializer.Serialize(request),
            Processed = false,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.TransactionalInbox.Add(inboxMessage);
        await dbContext.SaveChangesAsync();

        await service.ProcessPaymentAsync(inboxMessage);

        var account = await service.GetAccountAsync("user-1");
        Assert.NotNull(account);
        Assert.Equal(50, account.Balance);

        var transaction = await dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.OrderId == request.OrderId);
        Assert.NotNull(transaction);
        Assert.False(transaction.Success);
    }

    [Fact]
    public async Task ProcessPayment_ShouldBeIdempotent()
    {
        var dbContext = GetDbContext();
        var logger = new Mock<ILogger<PaymentService>>();
        var messagePublisher = new Mock<IMessagePublisher>();
        var service = new PaymentService(dbContext, messagePublisher.Object, logger.Object);

        await service.CreateAccountAsync("user-1");
        await service.TopUpAccountAsync("user-1", 100);

        var orderId = Guid.NewGuid();
        var request = new OrderPaymentRequest
        {
            OrderId = orderId,
            UserId = "user-1",
            Amount = 50,
            Description = "Test order"
        };

        var inboxMessage = new TransactionalInbox
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid().ToString(),
            MessageType = typeof(OrderPaymentRequest).Name,
            Payload = System.Text.Json.JsonSerializer.Serialize(request),
            Processed = false,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.TransactionalInbox.Add(inboxMessage);
        await dbContext.SaveChangesAsync();

        await service.ProcessPaymentAsync(inboxMessage);
        await service.ProcessPaymentAsync(inboxMessage);

        var transactions = await dbContext.PaymentTransactions
            .Where(t => t.OrderId == orderId)
            .ToListAsync();
        Assert.Single(transactions);
        Assert.Equal(50, (await service.GetAccountAsync("user-1"))!.Balance);
    }
}
