namespace NutriCare.Domain.Interfaces;

using NutriCare.Domain.Entities;

public interface IPackageCreditService
{
    Task<PackageEnrollment> IssueFromVerifiedPaymentAsync(long paymentId, long actorUserId, CancellationToken ct = default);
    Task<PackageCreditUsage> ReserveAppointmentAsync(long enrollmentId, long appointmentId, string kind, long actorUserId, CancellationToken ct = default);
    Task ValidateRescheduleAsync(Appointment appointment, CancellationToken ct = default);
    Task ApplyAppointmentStatusAsync(Appointment appointment, long actorUserId, string actor, CancellationToken ct = default);
    Task<PackageCreditUsage> ConsumePlanVersionAsync(long enrollmentId, long versionId, bool isRevision, long actorUserId, CancellationToken ct = default);
    Task<PackageEnrollment> ExtendAsync(long enrollmentId, int days, string reason, byte[] expectedRowVersion, long actorUserId, CancellationToken ct = default);
}
