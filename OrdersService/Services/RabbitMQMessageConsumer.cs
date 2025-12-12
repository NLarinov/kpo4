using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrdersService.Data;
using OrdersService.Models;
using OrdersService.Services;
using Shared.Messages;
using RabbitMQ.Client;

namespace OrdersService.Services;

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

        var consumer = new RabbitMQ.Client.Events.EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            _logger.LogInformation("Received message from queue {QueueName}", queueName);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

                var result = JsonSerializer.Deserialize<PaymentResult>(message);
                if (result != null)
                {
                    var status = result.Success ? OrderStatus.FINISHED : OrderStatus.CANCELED;
                    await orderService.UpdateOrderStatusAsync(result.OrderId, status);
                    _logger.LogInformation("Order {OrderId} status updated to {Status}", result.OrderId, status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment result message");
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
