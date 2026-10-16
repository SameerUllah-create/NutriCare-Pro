namespace NutriCare.Tests.Integrations;

using NutriCare.Application.Handlers.Chat;
using NutriCare.Application.Handlers.VideoConsultations;
using NutriCare.Application.Queries.Chat;
using NutriCare.Application.Queries.VideoConsultations;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public class CommunicationAuthorizationTests
{
    [Fact]
    public async Task RevokedAssignmentOverridesLegacyAppointmentForChat()
    {
        var users = new MemoryRepository<User>(); users.Items.AddRange([new User { Id = 1 }, new User { Id = 2 }]);
        var patients = new MemoryRepository<Patient>(); patients.Items.Add(new Patient { Id = 10, UserId = 1 });
        var clinicians = new MemoryRepository<Nutritionist>(); clinicians.Items.Add(new Nutritionist { Id = 20, UserId = 2 });
        var appointments = new MemoryRepository<Appointment>(); appointments.Items.Add(new Appointment { Id = 30, PatientId = 10, NutritionistId = 20, Status = "Confirmed" });
        var assignments = new MemoryRepository<ClinicalAssignment>(); assignments.Items.Add(new ClinicalAssignment { PatientId = 10, NutritionistId = 20, StartedAt = DateTime.UtcNow.AddDays(-2), EndedAt = DateTime.UtcNow.AddDays(-1) });
        var handler = new EnsureChatAccessQueryHandler(users, patients, clinicians, appointments, new MemoryRepository<DietPlan>(), assignments);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new EnsureChatAccessQuery { CurrentUserId = 1, OtherUserId = 2 }, default));
    }

    [Fact]
    public async Task ExplicitActiveAssignmentAllowsChatBeforeFirstAppointment()
    {
        var users = new MemoryRepository<User>(); users.Items.AddRange([new User { Id = 1 }, new User { Id = 2 }]);
        var patients = new MemoryRepository<Patient>(); patients.Items.Add(new Patient { Id = 10, UserId = 1 });
        var clinicians = new MemoryRepository<Nutritionist>(); clinicians.Items.Add(new Nutritionist { Id = 20, UserId = 2 });
        var assignments = new MemoryRepository<ClinicalAssignment>(); assignments.Items.Add(new ClinicalAssignment { PatientId = 10, NutritionistId = 20, StartedAt = DateTime.UtcNow.AddMinutes(-1) });
        var handler = new EnsureChatAccessQueryHandler(users, patients, clinicians, new MemoryRepository<Appointment>(), new MemoryRepository<DietPlan>(), assignments);
        Assert.True(await handler.Handle(new EnsureChatAccessQuery { CurrentUserId = 1, OtherUserId = 2 }, default));
    }

    [Fact]
    public async Task VideoDetailsNeverExposeHostOrDirectJoinUrls()
    {
        var appointments = new MemoryRepository<Appointment>(); appointments.Items.Add(new Appointment { Id = 1, Status = "Confirmed" });
        var consultations = new MemoryRepository<VideoConsultation>(); consultations.Items.Add(new VideoConsultation { Id = 2, AppointmentId = 1, MeetingUrl = "https://meet.example.test/secret", HostUrl = "https://zoom.example.test/host-secret" });
        var handler = new GetVideoConsultationByIdQueryHandler(appointments, consultations, new MemoryRepository<Patient>(), new MemoryRepository<Nutritionist>(), new AllowClinicalAccess());
        var result = await handler.Handle(new GetVideoConsultationByIdQuery { ConsultationId = 2, RequestedByUserId = 1, RequestedByRole = "Patient" }, default);
        Assert.Null(result.HostUrl); Assert.Null(result.MeetingUrl);
    }

    [Fact]
    public async Task CancelledAppointmentCannotBeJoinedEvenIfConsultationWasNotCancelled()
    {
        var appointments = new MemoryRepository<Appointment>(); appointments.Items.Add(new Appointment { Id = 1, Status = "Cancelled" });
        var consultations = new MemoryRepository<VideoConsultation>(); consultations.Items.Add(new VideoConsultation { Id = 2, AppointmentId = 1, Status = "Scheduled", MeetingUrl = "https://meet.example.test/secret", ScheduledStartTime = DateTime.UtcNow, ScheduledEndTime = DateTime.UtcNow.AddMinutes(30) });
        var handler = new JoinVideoConsultationCommandHandler(appointments, consultations, new MemoryRepository<Patient>(), new MemoryRepository<Nutritionist>(), new AllowClinicalAccess(), new MemoryUnitOfWork(), new AllowConsent());
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new NutriCare.Application.Commands.VideoConsultations.JoinVideoConsultationCommand { ConsultationId = 2, RequestedByUserId = 1, RequestedByRole = "Patient" }, default));
    }

    private sealed class AllowClinicalAccess : IClinicalAccessService
    {
        public Task<bool> CanAccessPatientAsync(long patientId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task EnsurePatientAccessAsync(long patientId, bool allowPatient = true, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureAssignedClinicianAsync(long patientId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureAppointmentAccessAsync(long appointmentId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<long> GetNutritionistIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(20L);
        public Task<long> GetPatientIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(10L);
    }

    private sealed class AllowConsent : IClinicalConsentService
    {
        public Task<ConsentRecord> RequireAsync(long userId, string purpose, long? consentRecordId = null, CancellationToken cancellationToken = default) => Task.FromResult(new ConsentRecord { UserId = userId, Purpose = purpose });
    }
}
