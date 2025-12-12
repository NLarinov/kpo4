namespace Shared.Messages;

public class PaymentResult
{
    public Guid OrderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
