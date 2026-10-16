namespace NutriCare.Tests.Clinical;

using System.Text.Json;
using NutriCare.Application.Commands.Appointments;
using NutriCare.Application.Commands.VideoConsultations;
using NutriCare.Application.Handlers.VideoConsultations;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Tests.Integrations;

public sealed class AppointmentPolicyRegressionTests
{
    [Theory]
    [InlineData("Online")]
    [InlineData("Video")]
    public async Task Either_service_online_choice_requires_teleconsultation_consent(string mode)
    {
        var f = new BookingFixture();
        f.Service.Mode = "Either";
        await Assert.ThrowsAsync<ArgumentException>(() => f.Workflow.BookAsync(f.Booking(mode), default));
        Assert.Empty(f.Appointments.Items);
        Assert.Empty(f.Outbox.Events);

        f.AcceptTeleconsultation();
        var booked = await f.Workflow.BookAsync(f.Booking(mode), default);
        Assert.Equal("Online", booked.AppointmentType);
        Assert.Equal("Online", Assert.Single(f.Appointments.Items).AppointmentType);
    }

    [Fact]
    public async Task Either_service_in_person_choice_does_not_require_remote_consent()
    {
        var f = new BookingFixture();
        f.Service.Mode = "Either";
        var booked = await f.Workflow.BookAsync(f.Booking("InPerson"), default);
        Assert.Equal("InPerson", booked.AppointmentType);
        Assert.Single(f.Appointments.Items);
        Assert.DoesNotContain(f.Consents.Items, c => c.Purpose == "Teleconsultation");
    }

    [Fact]
    public async Task Reusing_a_booking_key_cannot_change_the_chosen_mode()
    {
        var f = new BookingFixture();
        f.Service.Mode = "Either";
        var request = f.Booking("InPerson");
        await f.Workflow.BookAsync(request, default);
        request.AppointmentType = "Online";
        await Assert.ThrowsAsync<ConflictException>(() => f.Workflow.BookAsync(request, default));
        Assert.Equal("InPerson", Assert.Single(f.Appointments.Items).AppointmentType);
    }

    [Fact]
    public async Task Video_and_online_aliases_retry_the_same_concrete_booking()
    {
        var f = new BookingFixture();
        f.Service.Mode = "Either";
        f.AcceptTeleconsultation();
        var request = f.Booking("Video");
        var original = await f.Workflow.BookAsync(request, default);
        request.AppointmentType = "Online";
        var retry = await f.Workflow.BookAsync(request, default);
        Assert.Equal(original.Id, retry.Id);
        Assert.Single(f.Appointments.Items);
        Assert.Single(f.History.Items);
    }

    [Theory]
    [InlineData("Either", "Either")]
    [InlineData("Either", "")]
    [InlineData("Either", "Phone")]
    [InlineData("Online", "InPerson")]
    [InlineData("InPerson", "Online")]
    public async Task Ambiguous_or_unsupported_mode_is_rejected_before_booking(string offered, string selected)
    {
        var f = new BookingFixture();
        f.Service.Mode = offered;
        await Assert.ThrowsAsync<ArgumentException>(() => f.Workflow.BookAsync(f.Booking(selected), default));
        Assert.Empty(f.Appointments.Items);
        Assert.Empty(f.History.Items);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("unconfirmed")]
    [InlineData("deleted")]
    [InlineData("unverified-professional")]
    public async Task Unavailable_clinician_cannot_receive_a_booking(string condition)
    {
        var f = new BookingFixture();
        var account = f.Users.Items.Single(u => u.Id == 2);
        switch (condition)
        {
            case "missing": f.Users.Items.Remove(account); break;
            case "inactive": account.IsActive = false; break;
            case "unconfirmed": account.EmailConfirmed = false; break;
            case "deleted": account.DeletedAt = DateTime.UtcNow; break;
            case "unverified-professional": f.Nutritionists.Items[0].IsVerified = false; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => f.Workflow.BookAsync(f.Booking("InPerson"), default));
        Assert.Empty(f.Appointments.Items);
        Assert.Empty(f.Outbox.Events);
    }

    [Fact]
    public async Task Earlier_reschedules_do_not_use_the_short_notice_quota()
    {
        var f = new BookingFixture();
        var appointment = f.ExistingAppointment(DateTime.UtcNow.AddHours(12));
        appointment.RescheduleCount = 5;
        for (var i = 0; i < 5; i++) f.RecordReschedule(appointment.Id, 48);

        var changed = await f.Workflow.RescheduleAsync(f.Reschedule(appointment.Id), default);
        Assert.Equal(6, changed.RescheduleCount);
        Assert.Equal(6, f.History.Items.Count);
        Assert.Contains(f.Outbox.Events, e => e.Contains("rescheduled:6", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Used_short_notice_quota_blocks_another_short_notice_change()
    {
        var f = new BookingFixture();
        var appointment = f.ExistingAppointment(DateTime.UtcNow.AddHours(12));
        appointment.RescheduleCount = 1;
        f.RecordReschedule(appointment.Id, 8);

        await Assert.ThrowsAsync<ConflictException>(() => f.Workflow.RescheduleAsync(f.Reschedule(appointment.Id), default));
        Assert.Equal(1, appointment.RescheduleCount);
        Assert.Single(f.History.Items);
        Assert.Empty(f.Outbox.Events);
    }

    [Fact]
    public async Task Used_short_notice_quota_does_not_block_a_change_with_24_hours_notice()
    {
        var f = new BookingFixture();
        var appointment = f.ExistingAppointment(DateTime.UtcNow.AddHours(30));
        appointment.RescheduleCount = 1;
        f.RecordReschedule(appointment.Id, 8);

        var changed = await f.Workflow.RescheduleAsync(f.Reschedule(appointment.Id), default);
        Assert.Equal(2, changed.RescheduleCount);
        Assert.Equal(2, f.History.Items.Count);
    }

    [Fact]
    public async Task Rescheduling_under_six_hours_is_rejected_even_with_unused_quota()
    {
        var f = new BookingFixture();
        var appointment = f.ExistingAppointment(DateTime.UtcNow.AddHours(5));
        await Assert.ThrowsAsync<ConflictException>(() => f.Workflow.RescheduleAsync(f.Reschedule(appointment.Id), default));
        Assert.Equal(0, appointment.RescheduleCount);
        Assert.Empty(f.History.Items);
    }

    [Fact]
    public async Task In_person_appointment_cannot_create_a_provider_video_meeting()
    {
        var f = new BookingFixture();
        var appointment = f.ExistingAppointment(DateTime.UtcNow.AddDays(1));
        appointment.Status = "Confirmed";
        var provider = new NoVideoProvider();
        var handler = new ScheduleVideoConsultationCommandHandler(f.Appointments, new MemoryRepository<VideoConsultation>(),
            f.Patients, f.Nutritionists, new TestAccess(), provider, new TestConsent(), new MemoryUnitOfWork());

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new ScheduleVideoConsultationCommand
            { AppointmentId = appointment.Id, RequestedByUserId = 2, RequestedByRole = "Nutritionist", Provider = "Zoom" }, default));
        Assert.Equal(0, provider.Calls);
        Assert.Equal("InPerson", appointment.AppointmentType);
    }

    private sealed class BookingFixture
    {
        public MemoryRepository<Appointment> Appointments { get; } = new();
        public MemoryRepository<Patient> Patients { get; } = new();
        public MemoryRepository<Nutritionist> Nutritionists { get; } = new();
        public MemoryRepository<User> Users { get; } = new();
        public MemoryRepository<IntakeForm> Intakes { get; } = new();
        public MemoryRepository<ConsentRecord> Consents { get; } = new();
        public MemoryRepository<PolicyDocument> Policies { get; } = new();
        public MemoryRepository<AppointmentHistory> History { get; } = new();
        public MemoryRepository<CancellationPolicy> CancellationPolicies { get; } = new();
        public RecordingOutbox Outbox { get; } = new();
        public Service Service { get; } = new() { Id = 30, NutritionistId = 20, Mode = "InPerson", DurationMinutes = 45, Price = 2500, Currency = "PKR" };
        public ClinicalAppointmentWorkflow Workflow { get; }

        public BookingFixture()
        {
            Patients.Items.Add(new Patient { Id = 10, UserId = 1 });
            Nutritionists.Items.Add(new Nutritionist { Id = 20, UserId = 2, IsVerified = true });
            Users.Items.AddRange([new User { Id = 1, EmailConfirmed = true }, new User { Id = 2, EmailConfirmed = true }]);
            Policies.Items.Add(new PolicyDocument { Id = 40, Purpose = "HealthData", Version = "1", Content = "Approved health consent", IsPublished = true });
            Consents.Items.Add(new ConsentRecord { Id = 50, UserId = 1, Purpose = "HealthData", PolicyVersion = "1", ConsentText = "Approved health consent" });
            Intakes.Items.Add(new IntakeForm { Id = 60, PatientId = 10, ConsentRecordId = 50 });
            CancellationPolicies.Items.Add(new CancellationPolicy { Id = 70, Version = 1, IsPublished = true, Status = "Published", FullRefundHours = 24, FreeRescheduleHours = 6, MaxFreeReschedules = 1 });
            Workflow = new(Appointments, Patients, Nutritionists, Intakes, Consents, Policies, new MemoryRepository<ClinicalAssignment>(),
                History, new MemoryRepository<ConsultationNote>(), CancellationPolicies, new MemoryRepository<VideoConsultation>(), Users,
                new TestAccess(), new AvailableService(Service), new SerialClinicalLock(), Outbox, new TestUser(), new MemoryUnitOfWork());
        }

        public CreateAppointmentCommand Booking(string mode) => new() { PatientId = 10, NutritionistId = 20, ServiceId = 30, IntakeFormId = 60,
            AppointmentDateTime = DateTime.UtcNow.AddDays(7), AppointmentType = mode, IdempotencyKey = "isolated-mode-test" };

        public RescheduleAppointmentCommand Reschedule(long id) => new() { AppointmentId = id, PatientId = 10, RequestedByUserId = 1, AppointmentDateTime = DateTime.UtcNow.AddDays(7) };

        public Appointment ExistingAppointment(DateTime start)
        {
            var appointment = new Appointment { Id = 80, PatientId = 10, NutritionistId = 20, ServiceId = 30, AppointmentDateTime = start,
                AppointmentType = "InPerson", Status = "Confirmed", Duration = 45, CancellationPolicyId = 70, CancellationPolicyVersion = 1,
                CancellationPolicySnapshotJson = JsonSerializer.Serialize(CancellationPolicies.Items.Single()) };
            Appointments.Items.Add(appointment); return appointment;
        }

        public void RecordReschedule(long appointmentId, int noticeHours)
        {
            var changedAt = DateTime.UtcNow.AddDays(-7);
            History.Items.Add(new AppointmentHistory { AppointmentId = appointmentId, ChangedByUserId = 1, FromStatus = "Confirmed", ToStatus = "Pending",
                PreviousStartAt = changedAt.AddHours(noticeHours), NewStartAt = changedAt.AddDays(14), CreatedAt = changedAt, Reason = "Patient reschedule" });
        }

        public void AcceptTeleconsultation()
        {
            Policies.Items.Add(new PolicyDocument { Id = 41, Purpose = "Teleconsultation", Version = "1", Content = "Approved remote consent", IsPublished = true });
            Consents.Items.Add(new ConsentRecord { Id = 51, UserId = 1, Purpose = "Teleconsultation", PolicyVersion = "1", ConsentText = "Approved remote consent" });
        }
    }

    private sealed class AvailableService(Service service) : IClinicalSchedulingService
    {
        public Task<IReadOnlyCollection<DateTime>> GetAvailableSlotsAsync(long nutritionistId, long? serviceId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<DateTime>>([fromUtc]);
        public Task<Service> ValidateSlotAsync(long nutritionistId, long? serviceId, DateTime startUtc, long? ignoredAppointmentId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(service);
        public Task<int> GetBookingBufferAsync(long nutritionistId, long? serviceId, DateTime startUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(service.BookingBufferMinutes);
    }

    private sealed class NoVideoProvider : IVideoMeetingService
    {
        public int Calls { get; private set; }
        public Task<VideoMeetingCreateResult> CreateMeetingAsync(string provider, VideoMeetingCreateRequest request, CancellationToken cancellationToken = default)
        { Calls++; throw new InvalidOperationException("In-person appointments must never call a video provider"); }
    }
}
