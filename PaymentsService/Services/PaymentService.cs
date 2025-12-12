using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Models;
using Shared.Messages;

namespace PaymentsService.Services;

public class PaymentService : IPaymentService
{
    private readonly PaymentsDbContext _dbContext;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        PaymentsDbContext dbContext,
        IMessagePublisher messagePublisher,
        ILogger<PaymentService> logger)
    {
        _dbContext = dbContext;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    public async Task<Account?> CreateAccountAsync(string userId)
    {
        var existing = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (existing != null)
        {
            return null; // Уже существует
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();
        return account;
    }

    public async Task<Account?> GetAccountAsync(string userId)
    {
        return await _dbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
    }

    public async Task<Account?> TopUpAccountAsync(string userId, decimal amount)
    {
        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (account == null)
        {
            return null;
        }

        account.Balance += amount;
        account.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return account;
    }

    public async Task ProcessPaymentAsync(TransactionalInbox inboxMessage)
    {
        if (inboxMessage.Processed)
        {
            _logger.LogInformation("Message {MessageId} already processed", inboxMessage.MessageId);
            return;
        }

        try
        {
            var request = JsonSerializer.Deserialize<OrderPaymentRequest>(inboxMessage.Payload);
            if (request == null)
            {
                throw new InvalidOperationException("Invalid payment request");
            }

            var existingTransaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(t => t.OrderId == request.OrderId);

            if (existingTransaction != null)
            {
                _logger.LogInformation("Payment for order {OrderId} already processed", request.OrderId);
                inboxMessage.Processed = true;
                inboxMessage.ProcessedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                await PublishPaymentResultAsync(request.OrderId, request.UserId, existingTransaction.Success, existingTransaction.ErrorMessage);
                return;
            }

            var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == request.UserId);
            if (account == null)
            {
                await CreateFailedPaymentAsync(request, "Account not found");
                return;
            }

            if (account.Balance < request.Amount)
            {
                await CreateFailedPaymentAsync(request, "Insufficient funds");
                return;
            }

            account.Balance -= request.Amount;
            account.UpdatedAt = DateTime.UtcNow;

            var transaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                OrderId = request.OrderId,
                UserId = request.UserId,
                Amount = request.Amount,
                Success = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.PaymentTransactions.Add(transaction);
            inboxMessage.Processed = true;
            inboxMessage.ProcessedAt = DateTime.UtcNow;

            var result = new PaymentResult
            {
                OrderId = request.OrderId,
                UserId = request.UserId,
                Success = true
            };

            var outboxMessage = new TransactionalOutbox
            {
                Id = Guid.NewGuid(),
                MessageType = typeof(PaymentResult).Name,
                Payload = JsonSerializer.Serialize(result),
                Published = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.TransactionalOutbox.Add(outboxMessage);

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Payment processed successfully for order {OrderId}", request.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for message {MessageId}", inboxMessage.MessageId);
            throw;
        }
    }

    private async Task CreateFailedPaymentAsync(OrderPaymentRequest request, string errorMessage)
    {
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            UserId = request.UserId,
            Amount = request.Amount,
            Success = false,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.PaymentTransactions.Add(transaction);

        var result = new PaymentResult
        {
            OrderId = request.OrderId,
            UserId = request.UserId,
            Success = false,
            ErrorMessage = errorMessage
        };

        var outboxMessage = new TransactionalOutbox
        {
            Id = Guid.NewGuid(),
            MessageType = typeof(PaymentResult).Name,
            Payload = JsonSerializer.Serialize(result),
            Published = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TransactionalOutbox.Add(outboxMessage);
        await _dbContext.SaveChangesAsync();
    }

    private async Task PublishPaymentResultAsync(Guid orderId, string userId, bool success, string? errorMessage)
    {
        var result = new PaymentResult
        {
            OrderId = orderId,
            UserId = userId,
            Success = success,
            ErrorMessage = errorMessage
        };

        var outboxMessage = new TransactionalOutbox
        {
            Id = Guid.NewGuid(),
            MessageType = typeof(PaymentResult).Name,
            Payload = JsonSerializer.Serialize(result),
            Published = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TransactionalOutbox.Add(outboxMessage);
        await _dbContext.SaveChangesAsync();
    }
}
