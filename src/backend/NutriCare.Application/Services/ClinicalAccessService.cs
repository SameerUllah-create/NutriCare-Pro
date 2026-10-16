namespace NutriCare.Application.Services;

using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public sealed class ClinicalAccessService(
    ICurrentUserService currentUser,
    IRepository<Patient> patients,
    IRepository<Nutritionist> nutritionists,
    IRepository<ClinicalAssignment> assignments,
    IRepository<Appointment> appointments,
    IRepository<DietPlan> plans) : IClinicalAccessService
{
    public async Task<long> GetPatientIdAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.Role != "Patient") throw Denied();
        return (await patients.GetAsync(q => q.Where(p => p.UserId == currentUser.UserId && p.DeletedAt == null)))
            .FirstOrDefault()?.Id ?? throw Denied();
    }

    public async Task<long> GetNutritionistIdAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.Role != "Nutritionist") throw Denied();
        return (await nutritionists.GetAsync(q => q.Where(n => n.UserId == currentUser.UserId && n.DeletedAt == null)))
            .FirstOrDefault()?.Id ?? throw Denied();
    }

    public async Task<bool> CanAccessPatientAsync(long patientId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || patientId <= 0) return false;
        var patient = await patients.GetByIdAsync(patientId);
        if (patient == null || patient.DeletedAt != null) return false;
        if (currentUser.Role == "Patient") return patient.UserId == currentUser.UserId;
        if (currentUser.Role == "Admin") return currentUser.CanAccessClinicalData;
        if (currentUser.Role != "Nutritionist") return false;

        var clinicianId = await GetNutritionistIdAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var relationships = (await assignments.GetAsync(q => q.Where(a => a.PatientId == patientId &&
            a.NutritionistId == clinicianId))).ToArray();
        // Explicit revocation takes precedence over legacy association inference.
        if (relationships.Length > 0)
            return relationships.Any(a => a.DeletedAt == null && a.StartedAt <= now && (!a.EndedAt.HasValue || a.EndedAt > now));

        var visits = await appointments.GetAsync(q => q.Where(a => a.PatientId == patientId &&
            a.NutritionistId == clinicianId && a.DeletedAt == null &&
            (a.Status == "Confirmed" || a.Status == "Approved" || a.Status == "Completed")));
        if (visits.Any()) return true;
        var assignedPlans = await plans.GetAsync(q => q.Where(p => p.PatientId == patientId &&
            p.NutritionistId == clinicianId && p.DeletedAt == null && p.PublishedAt != null));
        return assignedPlans.Any();
    }

    public async Task EnsurePatientAccessAsync(long patientId, bool allowPatient = true, CancellationToken cancellationToken = default)
    {
        if ((!allowPatient && currentUser.Role == "Patient") || !await CanAccessPatientAsync(patientId, cancellationToken))
            throw Denied();
    }

    public async Task EnsureAssignedClinicianAsync(long patientId, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role != "Nutritionist" || !await CanAccessPatientAsync(patientId, cancellationToken)) throw Denied();
    }

    public async Task EnsureAppointmentAccessAsync(long appointmentId, CancellationToken cancellationToken = default)
    {
        var appointment = await appointments.GetByIdAsync(appointmentId);
        if (appointment == null || appointment.DeletedAt != null) throw new EntityNotFoundException(nameof(Appointment), appointmentId);
        if (currentUser.Role == "Patient" && appointment.PatientId == await GetPatientIdAsync(cancellationToken)) return;
        if (currentUser.Role == "Nutritionist" && appointment.NutritionistId == await GetNutritionistIdAsync(cancellationToken) &&
            await CanAccessPatientAsync(appointment.PatientId, cancellationToken)) return;
        if (currentUser.Role == "Admin" && currentUser.CanAccessClinicalData) return;
        throw Denied();
    }

    private static ForbiddenAccessException Denied() => new("You do not have access to this clinical record");
}
