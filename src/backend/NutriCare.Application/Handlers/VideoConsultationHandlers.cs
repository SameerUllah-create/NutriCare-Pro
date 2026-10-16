namespace NutriCare.Application.Handlers.VideoConsultations;

using MediatR;
using NutriCare.Application.Commands.VideoConsultations;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.VideoConsultations;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public class ScheduleVideoConsultationCommandHandler : IRequestHandler<ScheduleVideoConsultationCommand, VideoConsultationDto>
{
    private readonly IClinicalConsentService _consent;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<VideoConsultation> _consultationRepository;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IClinicalAccessService _clinicalAccess;
    private readonly IVideoMeetingService _videoMeetingService;
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleVideoConsultationCommandHandler(
        IRepository<Appointment> appointmentRepository,
        IRepository<VideoConsultation> consultationRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository, IClinicalAccessService clinicalAccess,
        IVideoMeetingService videoMeetingService, IClinicalConsentService consent,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _clinicalAccess = clinicalAccess;
        _videoMeetingService = videoMeetingService;
        _consent = consent;
        _unitOfWork = unitOfWork;
    }

    public async Task<VideoConsultationDto> Handle(ScheduleVideoConsultationCommand request, CancellationToken cancellationToken)
    {
        var appointment = await VideoConsultationHelpers.GetRequiredAsync(_appointmentRepository, request.AppointmentId);
        await _clinicalAccess.EnsureAppointmentAccessAsync(appointment.Id, cancellationToken);
        if (appointment.AppointmentType is not ("Online" or "Video"))
            throw new ConflictException("Only online appointments can have a video session");
        var patient = await VideoConsultationHelpers.GetRequiredAsync(_patientRepository, appointment.PatientId);
        await _consent.RequireAsync(patient.UserId, "Teleconsultation", cancellationToken: cancellationToken);

        if (appointment.Status is not ("Confirmed" or "Approved"))
        {
            throw new ConflictException("Only confirmed appointments can have a video session");
        }

        var existing = await _consultationRepository.GetAsync(query => query.Where(v => v.AppointmentId == request.AppointmentId && v.DeletedAt == null));
        if (existing.Any(v => v.Status != "Cancelled"))
        {
            throw new ConflictException("A video consultation already exists for this appointment");
        }
        if (existing.Any(v => v.Status == "Cancelled" && v.ProviderSyncStatus != "Revoked"))
            throw new ConflictException("The previous provider meeting must be revoked before a replacement is created");

        var scheduledEnd = appointment.AppointmentDateTime.AddMinutes(appointment.Duration);
        var meeting = await _videoMeetingService.CreateMeetingAsync(
            request.Provider,
            new VideoMeetingCreateRequest(
                $"NutriCare consultation #{appointment.Id}",
                appointment.AppointmentDateTime,
                scheduledEnd,
                "UTC",
                appointment.Notes),
            cancellationToken);

        var consultation = existing.FirstOrDefault() ?? new VideoConsultation();
        consultation.AppointmentId = appointment.Id;
        consultation.Provider = meeting.Provider;
        consultation.ProviderMeetingId = meeting.ProviderMeetingId;
        consultation.RoomName = $"appointment-{appointment.Id}";
        consultation.MeetingUrl = meeting.MeetingUrl;
        consultation.HostUrl = meeting.HostUrl;
        consultation.JoinToken = null;
        consultation.ScheduledStartTime = appointment.AppointmentDateTime;
        consultation.ScheduledEndTime = scheduledEnd;
        consultation.Status = "Scheduled";
        consultation.CallStartTime = null;
        consultation.CallEndTime = null;
        consultation.CreatedByUserId = request.RequestedByUserId;
        consultation.ProviderResponse = "{}";
        consultation.ProviderSyncStatus = "Current";
        consultation.ProviderSyncAttempts = 0;
        consultation.ProviderSyncNextAttemptAt = null;
        consultation.Notes = request.Notes;

        if (consultation.Id == 0) await _consultationRepository.AddAsync(consultation);
        else await _consultationRepository.UpdateAsync(consultation);
        await _unitOfWork.SaveChangesAsync();

        return VideoConsultationHelpers.ToDto(consultation);
    }
}

public class JoinVideoConsultationCommandHandler : IRequestHandler<JoinVideoConsultationCommand, JoinVideoConsultationDto>
{
    private readonly IClinicalConsentService _consent;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<VideoConsultation> _consultationRepository;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IClinicalAccessService _clinicalAccess;
    private readonly IUnitOfWork _unitOfWork;

    public JoinVideoConsultationCommandHandler(
        IRepository<Appointment> appointmentRepository,
        IRepository<VideoConsultation> consultationRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository, IClinicalAccessService clinicalAccess,
        IUnitOfWork unitOfWork, IClinicalConsentService consent)
    {
        _appointmentRepository = appointmentRepository;
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _clinicalAccess = clinicalAccess;
        _unitOfWork = unitOfWork;
        _consent = consent;
    }

    public async Task<JoinVideoConsultationDto> Handle(JoinVideoConsultationCommand request, CancellationToken cancellationToken)
    {
        var consultation = await VideoConsultationHelpers.GetRequiredAsync(_consultationRepository, request.ConsultationId);
        var appointment = await VideoConsultationHelpers.GetRequiredAsync(_appointmentRepository, consultation.AppointmentId);
        await _clinicalAccess.EnsureAppointmentAccessAsync(appointment.Id, cancellationToken);

        if (appointment.Status is not ("Confirmed" or "Approved") || consultation.Status == "Completed" || consultation.Status == "Cancelled" || DateTime.UtcNow < consultation.ScheduledStartTime.AddMinutes(-15) || DateTime.UtcNow > consultation.ScheduledEndTime.AddMinutes(15) || string.IsNullOrWhiteSpace(consultation.MeetingUrl))
        {
            throw new ConflictException("This consultation is no longer joinable");
        }
        var patient = await VideoConsultationHelpers.GetRequiredAsync(_patientRepository, appointment.PatientId);
        await _consent.RequireAsync(patient.UserId, "Teleconsultation", cancellationToken: cancellationToken);

        if (consultation.CallStartTime == null)
        {
            consultation.CallStartTime = DateTime.UtcNow;
            consultation.Status = "InProgress";
            await _consultationRepository.UpdateAsync(consultation);
            await _unitOfWork.SaveChangesAsync();
        }

        var isHost = await VideoConsultationHelpers.IsNutritionistUserAsync(_nutritionistRepository, request.RequestedByUserId, appointment.NutritionistId);
        return new JoinVideoConsultationDto
        {
            ConsultationId = consultation.Id,
            AppointmentId = consultation.AppointmentId,
            Provider = consultation.Provider,
            MeetingUrl = consultation.MeetingUrl ?? string.Empty,
            HostUrl = isHost ? consultation.HostUrl : null,
            Status = consultation.Status,
            IsHost = isHost,
            ScheduledStartTime = consultation.ScheduledStartTime,
            ScheduledEndTime = consultation.ScheduledEndTime
        };
    }
}

public class CompleteVideoConsultationCommandHandler : IRequestHandler<CompleteVideoConsultationCommand, VideoConsultationDto>
{
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<VideoConsultation> _consultationRepository;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IClinicalAccessService _clinicalAccess;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteVideoConsultationCommandHandler(
        IRepository<Appointment> appointmentRepository,
        IRepository<VideoConsultation> consultationRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository, IClinicalAccessService clinicalAccess,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _clinicalAccess = clinicalAccess;
        _unitOfWork = unitOfWork;
    }

    public async Task<VideoConsultationDto> Handle(CompleteVideoConsultationCommand request, CancellationToken cancellationToken)
    {
        var consultation = await VideoConsultationHelpers.GetRequiredAsync(_consultationRepository, request.ConsultationId);
        var appointment = await VideoConsultationHelpers.GetRequiredAsync(_appointmentRepository, consultation.AppointmentId);
        await _clinicalAccess.EnsureAppointmentAccessAsync(appointment.Id, cancellationToken);
        if (consultation.Status != "InProgress") throw new ConflictException("Only an active consultation can be completed");
        if (!string.IsNullOrWhiteSpace(request.RecordingUrl)) throw new ValidationException("Recording links require a consented private recording workflow and are currently disabled");

        consultation.CallEndTime = DateTime.UtcNow;
        consultation.Duration = consultation.CallStartTime.HasValue
            ? (int)Math.Max(0, (consultation.CallEndTime.Value - consultation.CallStartTime.Value).TotalSeconds)
            : null;
        consultation.Status = "Completed";
        consultation.RecordingUrl = request.RecordingUrl;
        consultation.RecordingStatus = string.IsNullOrWhiteSpace(request.RecordingUrl) ? "NotRecorded" : "Completed";
        consultation.Notes = request.Notes ?? consultation.Notes;

        await _consultationRepository.UpdateAsync(consultation);
        await _unitOfWork.SaveChangesAsync();
        return VideoConsultationHelpers.ToDto(consultation);
    }
}

public class GetVideoConsultationByIdQueryHandler : IRequestHandler<GetVideoConsultationByIdQuery, VideoConsultationDto>
{
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<VideoConsultation> _consultationRepository;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IClinicalAccessService _clinicalAccess;

    public GetVideoConsultationByIdQueryHandler(
        IRepository<Appointment> appointmentRepository,
        IRepository<VideoConsultation> consultationRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository, IClinicalAccessService clinicalAccess)
    {
        _appointmentRepository = appointmentRepository;
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _clinicalAccess = clinicalAccess;
    }

    public async Task<VideoConsultationDto> Handle(GetVideoConsultationByIdQuery request, CancellationToken cancellationToken)
    {
        var consultation = await VideoConsultationHelpers.GetRequiredAsync(_consultationRepository, request.ConsultationId);
        var appointment = await VideoConsultationHelpers.GetRequiredAsync(_appointmentRepository, consultation.AppointmentId);
        await _clinicalAccess.EnsureAppointmentAccessAsync(appointment.Id, cancellationToken);
        return VideoConsultationHelpers.ToDto(consultation);
    }
}

public class GetVideoConsultationByAppointmentQueryHandler : IRequestHandler<GetVideoConsultationByAppointmentQuery, VideoConsultationDto>
{
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<VideoConsultation> _consultationRepository;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IClinicalAccessService _clinicalAccess;

    public GetVideoConsultationByAppointmentQueryHandler(
        IRepository<Appointment> appointmentRepository,
        IRepository<VideoConsultation> consultationRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository, IClinicalAccessService clinicalAccess)
    {
        _appointmentRepository = appointmentRepository;
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _clinicalAccess = clinicalAccess;
    }

    public async Task<VideoConsultationDto> Handle(GetVideoConsultationByAppointmentQuery request, CancellationToken cancellationToken)
    {
        var appointment = await VideoConsultationHelpers.GetRequiredAsync(_appointmentRepository, request.AppointmentId);
        await _clinicalAccess.EnsureAppointmentAccessAsync(appointment.Id, cancellationToken);
        var consultations = await _consultationRepository.GetAsync(query => query.Where(v => v.AppointmentId == request.AppointmentId && v.DeletedAt == null));
        var consultation = consultations.FirstOrDefault() ?? throw new EntityNotFoundException("VideoConsultation", request.AppointmentId);
        return VideoConsultationHelpers.ToDto(consultation);
    }
}

public class GetVideoConsultationHistoryQueryHandler : IRequestHandler<GetVideoConsultationHistoryQuery, IEnumerable<VideoConsultationDto>>
{
    private readonly IRepository<VideoConsultation> _consultationRepository;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IClinicalAccessService _clinicalAccess;

    public GetVideoConsultationHistoryQueryHandler(
        IRepository<VideoConsultation> consultationRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository, IClinicalAccessService clinicalAccess)
    {
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _clinicalAccess = clinicalAccess;
    }

    public async Task<IEnumerable<VideoConsultationDto>> Handle(GetVideoConsultationHistoryQuery request, CancellationToken cancellationToken)
    {
        var patientIds = new HashSet<long>();
        var nutritionistIds = new HashSet<long>();

        if (request.RequestedByRole == "Patient")
        {
            var patients = await _patientRepository.GetAsync(query => query.Where(p => p.UserId == request.RequestedByUserId && p.DeletedAt == null));
            patientIds.UnionWith(patients.Select(p => p.Id));
        }
        else if (request.RequestedByRole == "Nutritionist")
        {
            var nutritionists = await _nutritionistRepository.GetAsync(query => query.Where(n => n.UserId == request.RequestedByUserId && n.DeletedAt == null));
            nutritionistIds.UnionWith(nutritionists.Select(n => n.Id));
        }

        var consultations = await _consultationRepository.GetAsync(query =>
        {
            var filtered = query.Where(v => v.DeletedAt == null);
            if (request.RequestedByRole == "Patient")
            {
                filtered = filtered.Where(v => v.Appointment != null && patientIds.Contains(v.Appointment.PatientId));
            }
            else if (request.RequestedByRole == "Nutritionist")
            {
                filtered = filtered.Where(v => v.Appointment != null && nutritionistIds.Contains(v.Appointment.NutritionistId));
            }

            if (request.FromDate.HasValue)
            {
                filtered = filtered.Where(v => v.ScheduledStartTime >= request.FromDate.Value.Date);
            }

            if (request.ToDate.HasValue)
            {
                filtered = filtered.Where(v => v.ScheduledStartTime <= request.ToDate.Value.Date.AddDays(1).AddTicks(-1));
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                filtered = filtered.Where(v => v.Status == request.Status);
            }

            return filtered.OrderByDescending(v => v.ScheduledStartTime);
        });

        var visible = new List<VideoConsultationDto>();
        foreach (var consultation in consultations)
        {
            await _clinicalAccess.EnsureAppointmentAccessAsync(consultation.AppointmentId, cancellationToken);
            visible.Add(VideoConsultationHelpers.ToDto(consultation));
        }
        return visible;
    }
}

internal static class VideoConsultationHelpers
{
    internal static async Task<T> GetRequiredAsync<T>(IRepository<T> repository, long id) where T : BaseEntity
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted)
        {
            throw new EntityNotFoundException(typeof(T).Name, id);
        }

        return entity;
    }

    internal static async Task<bool> IsNutritionistUserAsync(IRepository<Nutritionist> nutritionistRepository, long userId, long nutritionistId)
    {
        var nutritionists = await nutritionistRepository.GetAsync(query => query.Where(n => n.UserId == userId && n.Id == nutritionistId && n.DeletedAt == null));
        return nutritionists.Any();
    }

    internal static VideoConsultationDto ToDto(VideoConsultation consultation)
    {
        return new VideoConsultationDto
        {
            Id = consultation.Id,
            AppointmentId = consultation.AppointmentId,
            Provider = consultation.Provider,
            ProviderMeetingId = consultation.ProviderMeetingId,
            RoomName = consultation.RoomName,
            MeetingUrl = null,
            HostUrl = null,
            ScheduledStartTime = consultation.ScheduledStartTime,
            ScheduledEndTime = consultation.ScheduledEndTime,
            CallStartTime = consultation.CallStartTime,
            CallEndTime = consultation.CallEndTime,
            Duration = consultation.Duration,
            RecordingUrl = null,
            RecordingStatus = consultation.RecordingStatus,
            Status = consultation.Status,
            Notes = consultation.Notes,
            CreatedAt = consultation.CreatedAt,
            UpdatedAt = consultation.UpdatedAt
        };
    }
}
