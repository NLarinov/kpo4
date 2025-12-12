using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaymentsService.Data;
using PaymentsService.Services;
using Shared.Messages;

namespace PaymentsService.BackgroundServices;

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherService> _logger;
    private readonly TimeSpan _publishingInterval = TimeSpan.FromSeconds(5);

    public OutboxPublisherService(IServiceProvider serviceProvider, ILogger<OutboxPublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
                var messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

                var unpublishedMessages = await dbContext.TransactionalOutbox
                    .Where(x => !x.Published)
                    .OrderBy(x => x.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                foreach (var message in unpublishedMessages)
                {
                    try
                    {
                        if (message.MessageType == typeof(PaymentResult).Name)
                        {
                            var result = JsonSerializer.Deserialize<PaymentResult>(message.Payload);
                            if (result != null)
                            {
                                await messagePublisher.PublishAsync("payment_results", result);
                                message.Published = true;
                                message.PublishedAt = DateTime.UtcNow;
                                await dbContext.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation("Published payment result for order {OrderId}", result.OrderId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error publishing outbox message {MessageId}", message.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox publisher");
            }

            await Task.Delay(_publishingInterval, stoppingToken);
        }
    }
}
