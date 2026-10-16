namespace NutriCare.Application.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;

public sealed class CancellationRefundOutbox(IRepository<CancellationRefundInstruction> instructions,
    IRepository<AuditLog> audits) : ICancellationRefundOutbox
{
    public async Task StageAsync(Appointment appointment, long actorUserId, string actorRole, DateTime cancelledAtUtc,
        CancellationToken cancellationToken = default)
    {
        if ((await instructions.GetAsync(q => q.Where(i => i.AppointmentId == appointment.Id))).Any()) return;
        var decision = Evaluate(appointment, actorRole, cancelledAtUtc);
        var item = new CancellationRefundInstruction
        {
            AppointmentId = appointment.Id, CancelledByUserId = actorUserId, CancellationActor = actorRole,
            CancelledAtUtc = cancelledAtUtc, PolicyId = appointment.CancellationPolicyId,
            PolicyVersion = appointment.CancellationPolicyVersion,
            PolicySnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(appointment.CancellationPolicySnapshotJson ?? ""))),
            Eligible = decision.Eligible, DecisionReason = decision.Reason, ExpectedAmount = appointment.PriceSnapshot,
            Currency = appointment.Currency, Status = decision.Eligible == true ? "Queued" : decision.Eligible == false ? "NotEligible" : "NeedsReview",
            NextAttemptAt = decision.Eligible == true ? DateTime.UtcNow : null
        };
        await instructions.AddAsync(item);
        if (appointment.IsPaid && decision.Eligible != false)
            appointment.PaymentStatus = decision.Eligible == true ? "RefundPending" : "RefundReviewRequired";
        await audits.AddAsync(new AuditLog { UserId = actorUserId, Action = "CancellationRefundDecision", EntityType = "Appointment",
            EntityId = appointment.Id, Description = decision.Reason,
            NewValues = JsonSerializer.Serialize(new { decision.Eligible, item.PolicyId, item.PolicyVersion, item.PolicySnapshotHash, cancelledAtUtc, actorRole }) });
    }

    public async Task WakeAsync(long appointmentId, CancellationToken cancellationToken = default)
    {
        var item = (await instructions.GetAsync(q => q.Where(i => i.AppointmentId == appointmentId && i.DeletedAt == null))).SingleOrDefault();
        if (item == null || item.Eligible != true) return;
        // Invalidate an in-flight summary: the worker must rescan after this event.
        item.Status = "Queued";
        item.LeaseToken = null; item.LeaseExpiresAt = null;
        item.NextAttemptAt = DateTime.UtcNow;
        await instructions.UpdateAsync(item);
    }

    public static (bool? Eligible, string Reason) Evaluate(Appointment appointment, string actorRole, DateTime cancelledAtUtc)
    {
        if (appointment.PackageEnrollmentId.HasValue) return (false, "PackageCreditNonMonetaryAdjustment");
        if (appointment.Status == "No-show") return (false, "NoShowNoAutomaticRefund");
        if (appointment.Status is not ("Cancelled" or "Rejected")) return (null, "AppointmentNotCancelled");
        if (actorRole is not ("Patient" or "Nutritionist")) return (null, "CancellationActorRequiresReview");
        CancellationPolicy? policy;
        try { policy = JsonSerializer.Deserialize<CancellationPolicy>(appointment.CancellationPolicySnapshotJson ?? "null"); }
        catch (JsonException) { return (null, "UntraceableBookedPolicy"); }
        if (policy == null || policy.Id != appointment.CancellationPolicyId || policy.Version != appointment.CancellationPolicyVersion ||
            !policy.IsPublished || policy.Status != "Published" || policy.ApprovedByUserId is not > 0 || !policy.ApprovedAt.HasValue ||
            policy.ApprovedAt > appointment.CreatedAt || policy.FullRefundHours < policy.FreeRescheduleHours || policy.FreeRescheduleHours < 0 ||
            (policy.EffectiveFrom.HasValue && policy.EffectiveFrom > appointment.CreatedAt) ||
            (policy.EffectiveTo.HasValue && policy.EffectiveTo <= appointment.CreatedAt)) return (null, "UnapprovedBookedPolicy");
        if (!appointment.PriceSnapshot.HasValue || appointment.PriceSnapshot <= 0 || appointment.Currency.Length != 3)
            return (null, "UntraceableBookedPrice");
        if (actorRole == "Nutritionist") return policy.ProviderCancellationRefund ? (true, "ApprovedProviderCancellation") : (false, "ProviderRefundNotApproved");
        if (appointment.Status == "Rejected") return (null, "PatientCannotRejectAppointment");
        var notice = appointment.AppointmentDateTime - cancelledAtUtc;
        if (notice < TimeSpan.FromHours(policy.FreeRescheduleHours)) return (false, "LateCancellationNoAutomaticRefund");
        return notice >= TimeSpan.FromHours(policy.FullRefundHours)
            ? (true, "ApprovedEarlyPatientCancellation") : (false, "ShortNoticeRescheduleOnly");
    }
}
