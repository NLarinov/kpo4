namespace OrdersService.Services;

public interface IMessageConsumer
{
    Task StartConsumingAsync(string queueName, CancellationToken cancellationToken);
}
