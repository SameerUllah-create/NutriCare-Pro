namespace NutriCare.Domain.Entities;

public class ProviderWebhookEvent : BaseEntity
{
    public string Provider { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public long PaymentId { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
