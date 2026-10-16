namespace NutriCare.Application.Services;

using System.Security.Cryptography;
using System.Text;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;

public sealed class CancellationRefundProcessor(IRepository<CancellationRefundInstruction> instructions,
    IRepository<Appointment> appointments, IRepository<Payment> payments, IRepository<PaymentRefund> refunds,
    IRepository<Patient> patients, IRepository<Nutritionist> nutritionists, IRepository<User> users,
    IRepository<AuditLog> audits, PaymentRefundWorkflow refundWorkflow, INotificationOutbox notifications, IUnitOfWork unit)
{
    public async Task ProcessAsync(long instructionId, CancellationToken cancellationToken, bool readOnlyRecovery = false)
    {
        CancellationRefundInstruction item; Appointment appointment; var token = Guid.NewGuid().ToString("N");
        await unit.BeginTransactionAsync($"cancellation-refund:{instructionId}");
        try
        {
            item = await instructions.GetByIdAsync(instructionId) ?? throw new InvalidOperationException("Cancellation work was not found");
            await instructions.ReloadAsync(item);
            if (item.DeletedAt != null || item.Eligible != true || item.Status == "NotEligible" ||
                ((item.Status is "Completed" or "NeedsReview") && !readOnlyRecovery) ||
                item.LeaseExpiresAt > DateTime.UtcNow || (!readOnlyRecovery && item.NextAttemptAt > DateTime.UtcNow))
            { await unit.CommitAsync(); return; }
            appointment = await appointments.GetByIdAsync(item.AppointmentId) ?? throw new InvalidOperationException("Cancellation appointment was not found");
            await appointments.ReloadAsync(appointment);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(appointment.CancellationPolicySnapshotJson ?? "")));
            var decision = CancellationRefundOutbox.Evaluate(appointment, item.CancellationActor, item.CancelledAtUtc);
            var actorMatches = item.CancellationActor == "Patient"
                ? (await patients.GetByIdAsync(appointment.PatientId))?.UserId == item.CancelledByUserId
                : (await nutritionists.GetByIdAsync(appointment.NutritionistId))?.UserId == item.CancelledByUserId;
            var recordedTime = appointment.Status == "Rejected" ? appointment.RejectedAt : appointment.CancelledAt;
            if (appointment.DeletedAt != null || decision.Eligible != true || !actorMatches || recordedTime != item.CancelledAtUtc ||
                hash != item.PolicySnapshotHash || item.PolicyId != appointment.CancellationPolicyId || item.PolicyVersion != appointment.CancellationPolicyVersion ||
                item.ExpectedAmount != appointment.PriceSnapshot || item.Currency != appointment.Currency)
            {
                item.Status = "NeedsReview"; item.LastErrorCode = "CancellationEvidenceMismatch"; item.NextAttemptAt = null;
                await AuditAsync(item, "CancellationRefundNeedsReview", item.LastErrorCode);
                await instructions.UpdateAsync(item); await unit.CommitAsync(); return;
            }
            item.Status = "Processing"; item.LeaseToken = token; item.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(5);
            item.NextAttemptAt = item.LeaseExpiresAt; item.AttemptCount++;
            await instructions.UpdateAsync(item); await unit.CommitAsync();
        }
        catch { await unit.RollbackAsync(); throw; }

        string? error = null; var contacted = false; long? lastPayment = null, lastRefund = null;
        try
        {
            var related = await payments.GetAsync(q => q.Where(p => p.RelatedToType == "Appointment" && p.RelatedToId == item.AppointmentId && p.DeletedAt == null));
            foreach (var payment in related.Where(p => p.Status is "Completed" or "PartialRefund" or "Refunded"))
            {
                await payments.ReloadAsync(payment);
                if (payment.PatientId != appointment.PatientId || payment.Amount != item.ExpectedAmount || payment.Currency != item.Currency)
                { error = "CapturedPaymentMismatch"; break; }
                lastPayment = payment.Id;
                var pending = (await refunds.GetAsync(q => q.Where(r => r.PaymentId == payment.Id && r.Status == "Pending" && r.DeletedAt == null))).ToArray();
                foreach (var refund in pending)
                {
                    contacted = true; lastRefund = refund.Id;
                    await refundWorkflow.ReconcileAsync(payment.Id, refund.Id, cancellationToken, allowCreate: !readOnlyRecovery);
                }
                if (!readOnlyRecovery)
                {
                    contacted = true;
                    var refund = await refundWorkflow.RefundCancellationAsync(payment.Id, item.Id, cancellationToken);
                    lastRefund = refund?.Id ?? lastRefund;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { error = "ProviderRequestInterrupted"; }
        catch { error = "RefundProcessingUnavailable"; }

        // No instruction transaction is held while reading provider/payment state. A signed
        // callback invalidates our lease, so a later settlement cannot be lost in this summary.
        var allPayments = (await payments.GetAsync(q => q.Where(p => p.RelatedToType == "Appointment" && p.RelatedToId == item.AppointmentId && p.DeletedAt == null))).ToArray();
        foreach (var payment in allPayments) await payments.ReloadAsync(payment);
        var settled = allPayments.Where(p => p.Status is "Completed" or "PartialRefund" or "Refunded").ToArray();
        var paymentIds = settled.Select(p => p.Id).ToArray();
        var ledger = (await refunds.GetAsync(q => q.Where(r => paymentIds.Contains(r.PaymentId) && r.DeletedAt == null))).ToArray();
        foreach (var refund in ledger) await refunds.ReloadAsync(refund);
        var requiresReview = ledger.FirstOrDefault(r => (r.Status == "Pending" && (r.LastErrorCode is "ExpiredIdempotencyWindow" or "ProviderRefundMismatch" or "KnownProviderRefundMissing" or "ProviderActionRequired" or "RefundLedgerMismatch")) ||
            (r.Status == "Failed" && r.IdempotencyKey != null && r.IdempotencyKey.StartsWith($"cancel-{item.Id}-", StringComparison.Ordinal)));
        if (requiresReview != null) error = requiresReview.LastErrorCode ?? "AutomaticRefundFailed";
        var fullyRefunded = settled.Length > 0 && settled.All(p => ledger.Where(r => r.PaymentId == p.Id && r.Status == "Completed").Sum(r => r.Amount) == p.Amount);
        var pendingCheckout = allPayments.Any(p => p.Status == "Pending");
        if (!fullyRefunded && contacted && item.AttemptCount >= 12 && error == null) error = "ProviderConfirmationTimeout";
        var status = error != null && (requiresReview != null || error == "CapturedPaymentMismatch" || item.AttemptCount >= 12) ? "NeedsReview" :
            fullyRefunded && !pendingCheckout ? "Completed" : settled.Length == 0 ? "AwaitingSettlement" : "AwaitingProvider";
        if (readOnlyRecovery && status != "Completed") status = "NeedsReview";

        await unit.BeginTransactionAsync($"cancellation-refund:{instructionId}");
        try
        {
            await instructions.ReloadAsync(item);
            if (item.LeaseToken != token) { await unit.CommitAsync(); return; }
            item.Status = status; item.LastErrorCode = error;
            item.PaymentId = lastPayment; item.PaymentRefundId = lastRefund;
            item.LeaseToken = null; item.LeaseExpiresAt = null;
            if (!contacted && item.AttemptCount > 0) item.AttemptCount--;
            item.NextAttemptAt = status is "Completed" or "NeedsReview" ? null : DateTime.UtcNow.AddMinutes(status == "AwaitingSettlement" ? 60 : Math.Min(60, Math.Pow(2, Math.Min(item.AttemptCount, 6))));
            await instructions.UpdateAsync(item);
            await AuditAsync(item, "CancellationRefundStatus", error ?? status);
            if (status == "Completed")
            {
                var completionKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(",", settled.Select(p => p.Id).Order()))));
                var patient = await patients.GetByIdAsync(appointment.PatientId);
                var user = patient == null ? null : await users.GetByIdAsync(patient.UserId);
                if (user != null) await notifications.EnqueueAsync(new NotificationRecipient(user.Id, user.Email ?? "", user.PhoneNumber, ""),
                    new NotificationRequest("CancellationRefundConfirmed", "Cancellation refund confirmed", "Your cancellation refunds have been confirmed. Review the payment records in your secure portal.",
                        "Appointment", appointment.Id, "/payments", ["InApp", "Email"]),
                    $"cancellation:{item.Id}:refunded:{completionKey}", cancellationToken);
            }
            await unit.CommitAsync();
        }
        catch { await unit.RollbackAsync(); throw; }
    }

    private async Task AuditAsync(CancellationRefundInstruction item, string action, string description)
    { await audits.AddAsync(new AuditLog { Action = action, EntityType = "CancellationRefundInstruction", EntityId = item.Id, Description = description }); }
}
