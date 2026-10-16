namespace NutriCare.Domain.Interfaces;

/// <summary>Patient ownership and active care-team authorization shared by clinical workflows.</summary>
public interface IClinicalAccessService
{
    Task<bool> CanAccessPatientAsync(long patientId, CancellationToken cancellationToken = default);
    Task EnsurePatientAccessAsync(long patientId, bool allowPatient = true, CancellationToken cancellationToken = default);
    Task EnsureAssignedClinicianAsync(long patientId, CancellationToken cancellationToken = default);
    Task EnsureAppointmentAccessAsync(long appointmentId, CancellationToken cancellationToken = default);
    Task<long> GetNutritionistIdAsync(CancellationToken cancellationToken = default);
    Task<long> GetPatientIdAsync(CancellationToken cancellationToken = default);
}

public interface IClinicalSchedulingService
{
    Task<IReadOnlyCollection<DateTime>> GetAvailableSlotsAsync(long nutritionistId, long? serviceId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<Entities.Service> ValidateSlotAsync(long nutritionistId, long? serviceId, DateTime startUtc, long? ignoredAppointmentId = null, CancellationToken cancellationToken = default);
    Task<int> GetBookingBufferAsync(long nutritionistId, long? serviceId, DateTime startUtc, CancellationToken cancellationToken = default);
}

public interface IClinicalPdfRenderer
{
    Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default);
}

public interface IClinicalConsentService
{
    Task<Entities.ConsentRecord> RequireAsync(long userId, string purpose, long? consentRecordId = null, CancellationToken cancellationToken = default);
}
