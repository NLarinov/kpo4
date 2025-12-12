using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messages;

namespace PaymentsService.Services;

public class RabbitMQMessageConsumer : IMessageConsumer, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMQMessageConsumer> _logger;

    public RabbitMQMessageConsumer(IServiceProvider serviceProvider, ILogger<RabbitMQMessageConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
            Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    public Task StartConsumingAsync(string queueName, CancellationToken cancellationToken)
    {
        _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();

            _logger.LogInformation("Received message {MessageId} from queue {QueueName}", messageId, queueName);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

                var inboxMessage = new TransactionalInbox
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    MessageType = typeof(OrderPaymentRequest).Name,
                    Payload = message,
                    Processed = false,
                    CreatedAt = DateTime.UtcNow
                };

                var existing = await dbContext.TransactionalInbox
                    .FirstOrDefaultAsync(x => x.MessageId == messageId, cancellationToken);

                if (existing == null)
                {
                    dbContext.TransactionalInbox.Add(inboxMessage);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Message {MessageId} saved to inbox", messageId);
                }
                else
                {
                    _logger.LogInformation("Message {MessageId} already exists in inbox, skipping", messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", messageId);
            }

            _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
        };

        _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        _logger.LogInformation("Started consuming from queue {QueueName}", queueName);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
