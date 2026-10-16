namespace NutriCare.Application.Services;

using NutriCare.Application.Commands.Payments;
using NutriCare.Application.DTOs;
using NutriCare.Application.Handlers.Payments;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

/// <summary>Reserve in SQL, reconcile/contact the provider outside SQL, then merge fresh state.</summary>
public sealed class PaymentRefundWorkflow(IRepository<Payment> payments, IRepository<PaymentRefund> refunds,
    IRepository<Invoice> invoices, IRepository<Appointment> appointments, IPaymentGatewayService gateway,
    IPaymentRefundReconciliation reconciliation, IUnitOfWork unit, IRepository<AuditLog>? audits = null)
{
    public async Task<PaymentRefundDto> RefundAsync(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        PaymentHandlerHelpers.ValidateIdempotencyKey(request.IdempotencyKey);
        long refundId;
        await unit.BeginTransactionAsync($"payment:{request.PaymentId}");
        try
        {
            var payment = await PaymentHandlerHelpers.GetRequiredAsync(payments, request.PaymentId);
            await payments.ReloadAsync(payment);
            var refund = (await refunds.GetAsync(q => q.Where(r => r.PaymentId == payment.Id && r.IdempotencyKey == request.IdempotencyKey && r.DeletedAt == null))).SingleOrDefault();
            if (refund != null)
            {
                await refunds.ReloadAsync(refund);
                if (refund.Amount != request.Amount) throw new ConflictException("The refund key was already used for another amount");
            }
            else
            {
                if (payment.Status is not ("Completed" or "PartialRefund")) throw new ConflictException("Only settled payments can be refunded");
                var ledger = (await refunds.GetAsync(q => q.Where(r => r.PaymentId == payment.Id && r.DeletedAt == null))).ToArray();
                foreach (var entry in ledger) await refunds.ReloadAsync(entry);
                EnsureConsistentBalance(payment, ledger);
                var reserved = ledger.Where(r => r.Status is "Pending" or "Completed").Sum(r => r.Amount);
                if (request.Amount <= 0 || request.Amount > payment.Amount - reserved)
                    throw new ValidationException("Refund amount exceeds the available balance");
                refund = new PaymentRefund { PaymentId = payment.Id, Amount = request.Amount, Currency = payment.Currency,
                    Status = "Pending", Reason = request.Reason, IdempotencyKey = request.IdempotencyKey };
                await refunds.AddAsync(refund); await unit.SaveChangesAsync();
                await AuditAsync(refund.Id, "RefundReserved", "ManualRefundReserved");
            }
            refundId = refund.Id;
            await unit.CommitAsync();
        }
        catch { await unit.RollbackAsync(); throw; }
        return await ReconcileAsync(request.PaymentId, refundId, cancellationToken);
    }

    public async Task<PaymentRefundDto?> RefundCancellationAsync(long paymentId, long instructionId, CancellationToken cancellationToken)
    {
        long? refundId = null;
        var prefix = $"cancel-{instructionId}-payment-{paymentId}-part-";
        await unit.BeginTransactionAsync($"payment:{paymentId}");
        try
        {
            var payment = await PaymentHandlerHelpers.GetRequiredAsync(payments, paymentId);
            await payments.ReloadAsync(payment);
            if (payment.Status is not ("Completed" or "PartialRefund" or "Refunded")) throw new ConflictException("The cancellation payment has not settled");
            var ledger = (await refunds.GetAsync(q => q.Where(r => r.PaymentId == paymentId && r.DeletedAt == null))).ToArray();
            foreach (var entry in ledger) await refunds.ReloadAsync(entry);
            EnsureConsistentBalance(payment, ledger);
            var own = ledger.Where(r => r.IdempotencyKey != null && r.IdempotencyKey.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (own.Any(r => r.Status == "Failed")) throw new ConflictException("An automatic refund failed and requires review");
            var pending = own.SingleOrDefault(r => r.Status == "Pending");
            if (pending != null) refundId = pending.Id;
            else
            {
                var available = payment.Amount - ledger.Where(r => r.Status is "Pending" or "Completed").Sum(r => r.Amount);
                if (available > 0)
                {
                    var refund = new PaymentRefund { PaymentId = paymentId, Amount = available, Currency = payment.Currency, Status = "Pending",
                        IdempotencyKey = prefix + (own.Length + 1), Reason = "Approved appointment cancellation" };
                    await refunds.AddAsync(refund); await unit.SaveChangesAsync(); refundId = refund.Id;
                    await AuditAsync(refund.Id, "RefundReserved", "ApprovedCancellationRefundReserved");
                }
            }
            await unit.CommitAsync();
        }
        catch { await unit.RollbackAsync(); throw; }
        return refundId.HasValue ? await ReconcileAsync(paymentId, refundId.Value, cancellationToken) : null;
    }

    public async Task<PaymentRefundDto> ReconcileAsync(long paymentId, long refundId, CancellationToken cancellationToken, bool allowCreate = true)
    {
        Payment payment; PaymentRefund refund; string token = Guid.NewGuid().ToString("N");
        await unit.BeginTransactionAsync($"payment:{paymentId}");
        try
        {
            payment = await PaymentHandlerHelpers.GetRequiredAsync(payments, paymentId); await payments.ReloadAsync(payment);
            refund = await PaymentHandlerHelpers.GetRequiredAsync(refunds, refundId); await refunds.ReloadAsync(refund);
            if (refund.PaymentId != payment.Id) throw new ConflictException("Refund payment does not match");
            if (refund.Status is "Completed" or "Failed" || refund.ProcessingUntil > DateTime.UtcNow)
            { await unit.CommitAsync(); return PaymentHandlerHelpers.ToRefundDto(refund); }
            if (string.IsNullOrWhiteSpace(refund.IdempotencyKey))
                throw new ConflictException("This refund has no traceable operation identity");
            var ledger = (await refunds.GetAsync(q => q.Where(r => r.PaymentId == payment.Id && r.DeletedAt == null))).ToArray();
            foreach (var entry in ledger) await refunds.ReloadAsync(entry);
            if (payment.RefundedAmount != ledger.Where(r => r.Status == "Completed").Sum(r => r.Amount))
            {
                refund.LastErrorCode = "RefundLedgerMismatch";
                await refunds.UpdateAsync(refund); await AuditAsync(refund.Id, "RefundNeedsReview", refund.LastErrorCode);
                await unit.CommitAsync(); return PaymentHandlerHelpers.ToRefundDto(refund);
            }
            refund.ProcessingToken = token; refund.ProcessingUntil = DateTime.UtcNow.AddMinutes(5);
            refund.ProviderRequestStartedAt ??= refund.CreatedAt;
            await refunds.UpdateAsync(refund); await unit.CommitAsync();
        }
        catch { await unit.RollbackAsync(); throw; }

        PaymentGatewayRefundState? state = null; string? providerId = refund.ProviderRefundId; string? error = null;
        var operationKey = $"nc-refund-{payment.Id}-{refund.IdempotencyKey}";
        try
        {
            state = await reconciliation.FindAsync(payment.PaymentMethod, payment.ProviderPaymentId ?? "", operationKey, providerId, cancellationToken);
            if (state == null)
            {
                if (providerId != null) error = "KnownProviderRefundMissing";
                else if (DateTime.UtcNow - refund.ProviderRequestStartedAt!.Value >= TimeSpan.FromHours(23)) error = "ExpiredIdempotencyWindow";
                else if (!allowCreate) error = "ProviderConfirmationPending";
                else
                {
                    var result = await gateway.RefundAsync(payment.PaymentMethod, payment.ProviderPaymentId ?? "", refund.Amount,
                        payment.Currency, null, cancellationToken, operationKey);
                    providerId = result.ProviderRefundId;
                    state = await reconciliation.FindAsync(payment.PaymentMethod, payment.ProviderPaymentId ?? "", operationKey, providerId, cancellationToken);
                    if (state == null) error = "ProviderConfirmationPending";
                }
            }
            if (state != null && (state.ProviderPaymentId != payment.ProviderPaymentId || state.Amount != refund.Amount || state.Currency != refund.Currency ||
                (providerId != null && state.ProviderRefundId != providerId) ||
                (state.OperationKey != null && state.OperationKey != operationKey) || (providerId == null && state.OperationKey != operationKey)))
            { state = null; providerId = refund.ProviderRefundId; error = "ProviderRefundMismatch"; }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { error = "ProviderRequestInterrupted"; }
        catch { error = "ProviderUnavailable"; }

        await unit.BeginTransactionAsync($"payment:{paymentId}");
        try
        {
            await payments.ReloadAsync(payment); await refunds.ReloadAsync(refund);
            if (refund.ProcessingToken != token)
            { await unit.CommitAsync(); return PaymentHandlerHelpers.ToRefundDto(refund); }
            // A signed callback may have settled this row while provider I/O was in flight.
            if (refund.Status is not ("Completed" or "Failed"))
            {
                if (refund.ProviderRefundId != null && providerId != null && refund.ProviderRefundId != providerId)
                    error = "ProviderRefundMismatch";
                else
                {
                    refund.ProviderRefundId ??= providerId ?? state?.ProviderRefundId;
                    if (state != null && error == null)
                    {
                        refund.Status = PaymentHandlerHelpers.MapProviderStatus(state.Status);
                        if (refund.Status == "Completed") refund.RefundedAt ??= DateTime.UtcNow;
                        if (state.Status == "requires_action") error = "ProviderActionRequired";
                        if (refund.Status == "Failed") error = "ProviderRefundFailed";
                    }
                }
                refund.LastErrorCode = error;
            }
            refund.ProcessingToken = null; refund.ProcessingUntil = null;
            await refunds.UpdateAsync(refund); await unit.SaveChangesAsync();
            await PaymentHandlerHelpers.ReconcileRefundsAsync(payment, refunds, payments, invoices, appointments);
            await AuditAsync(refund.Id, "RefundReconciled", refund.LastErrorCode ?? refund.Status);
            await unit.CommitAsync();
            return PaymentHandlerHelpers.ToRefundDto(refund);
        }
        catch { await unit.RollbackAsync(); throw; }
    }

    private static void EnsureConsistentBalance(Payment payment, IEnumerable<PaymentRefund> ledger)
    {
        var rows = ledger.ToArray();
        if (rows.Any(r => r.Amount <= 0 || r.Currency != payment.Currency) ||
            rows.Where(r => r.Status is "Pending" or "Completed").Sum(r => r.Amount) > payment.Amount ||
            payment.RefundedAmount != rows.Where(r => r.Status == "Completed").Sum(r => r.Amount))
            throw new ConflictException("The refund ledger requires reconciliation before another reservation");
    }

    private Task AuditAsync(long refundId, string action, string description)
        => audits == null ? Task.CompletedTask : AddAuditAsync(refundId, action, description);
    private async Task AddAuditAsync(long refundId, string action, string description)
    { await audits!.AddAsync(new AuditLog { Action = action, EntityType = "PaymentRefund", EntityId = refundId, Description = description }); }
}
