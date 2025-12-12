using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaymentsService.Data;
using PaymentsService.Models;
using PaymentsService.Services;
using Shared.Messages;

namespace PaymentsService.BackgroundServices;

public class InboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxProcessorService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(5);

    public InboxProcessorService(IServiceProvider serviceProvider, ILogger<InboxProcessorService> logger)
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
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                var unprocessedMessages = await dbContext.TransactionalInbox
                    .Where(x => !x.Processed)
                    .OrderBy(x => x.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                foreach (var message in unprocessedMessages)
                {
                    try
                    {
                        await paymentService.ProcessPaymentAsync(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing inbox message {MessageId}", message.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in inbox processor");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }
    }
}
