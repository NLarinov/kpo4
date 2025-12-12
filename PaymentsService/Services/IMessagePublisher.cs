namespace PaymentsService.Services;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string queueName, T message);
}
