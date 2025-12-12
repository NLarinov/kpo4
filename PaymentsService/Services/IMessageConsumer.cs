namespace PaymentsService.Services;

public interface IMessageConsumer
{
    Task StartConsumingAsync(string queueName, CancellationToken cancellationToken);
}
