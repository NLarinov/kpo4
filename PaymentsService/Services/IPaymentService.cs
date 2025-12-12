using PaymentsService.Models;

namespace PaymentsService.Services;

public interface IPaymentService
{
    Task<Account?> CreateAccountAsync(string userId);
    Task<Account?> GetAccountAsync(string userId);
    Task<Account?> TopUpAccountAsync(string userId, decimal amount);
    Task ProcessPaymentAsync(TransactionalInbox inboxMessage);
}
