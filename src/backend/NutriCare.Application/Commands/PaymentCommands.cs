namespace NutriCare.Application.Commands.Payments;

using MediatR;
using NutriCare.Application.DTOs;

public class CreatePaymentCommand : IRequest<PaymentDto>
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public long PatientId { get; set; }
    public long? NutritionistId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = "Stripe";
    public string? Description { get; set; }
    public string? RelatedToType { get; set; }
    public long? RelatedToId { get; set; }
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
}

public class RefundPaymentCommand : IRequest<PaymentRefundDto>
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public long PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}

public class HandlePaymentWebhookCommand : IRequest<PaymentDto>
{
    public string Provider { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? Signature { get; set; }
}
