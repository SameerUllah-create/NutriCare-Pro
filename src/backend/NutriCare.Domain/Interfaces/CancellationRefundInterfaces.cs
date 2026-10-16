namespace NutriCare.Domain.Interfaces;

using NutriCare.Domain.Entities;

public interface ICancellationRefundOutbox
{
    /// <summary>Stage with the appointment transaction; never contact the payment provider.</summary>
    Task StageAsync(Appointment appointment, long actorUserId, string actorRole, DateTime cancelledAtUtc,
        CancellationToken cancellationToken = default);
    /// <summary>Wake existing cancellation work after an authenticated provider event.</summary>
    Task WakeAsync(long appointmentId, CancellationToken cancellationToken = default);
}

public record PaymentGatewayRefundState(string ProviderRefundId, string ProviderPaymentId, decimal Amount,
    string Currency, string Status, string? OperationKey);

/// <summary>Authenticated provider reads used to recover uncertain refund submissions.</summary>
public interface IPaymentRefundReconciliation
{
    /// <summary>Null means a complete provider listing found no matching operation, never a transport failure.</summary>
    Task<PaymentGatewayRefundState?> FindAsync(string provider, string providerPaymentId, string operationKey,
        string? providerRefundId = null, CancellationToken cancellationToken = default);
}
