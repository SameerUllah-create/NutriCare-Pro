namespace NutriCare.Application.Handlers.MedicalReports;

using MediatR;
using NutriCare.Application.Commands.MedicalReports;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.MedicalReports;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public class UploadMedicalReportCommandHandler : IRequestHandler<UploadMedicalReportCommand, MedicalReportDto>
{
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<MedicalReport> _reportRepository;
    private readonly IClinicalAccessService _access;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClinicalConsentService _consent;

    public UploadMedicalReportCommandHandler(
        IRepository<Patient> patientRepository,
        IRepository<MedicalReport> reportRepository,
        IBlobStorageService blobStorageService,
        IUnitOfWork unitOfWork, IClinicalAccessService access, IClinicalConsentService consent)
    {
        _patientRepository = patientRepository;
        _reportRepository = reportRepository;
        _access = access;
        _blobStorageService = blobStorageService;
        _unitOfWork = unitOfWork;
        _consent = consent;
    }

    public async Task<MedicalReportDto> Handle(UploadMedicalReportCommand request, CancellationToken cancellationToken)
    {
        if (request.PatientId != await _access.GetPatientIdAsync(cancellationToken)) throw new ForbiddenAccessException("You can only upload your own report");
        await _consent.RequireAsync(request.UploadedByUserId, "HealthData", cancellationToken: cancellationToken);
        await MedicalReportHandlerHelpers.GetRequiredAsync(_patientRepository, request.PatientId);

        var upload = await _blobStorageService.UploadAsync(
            request.Content,
            request.OriginalFileName,
            request.ContentType,
            $"patients/{request.PatientId}/medical-reports",
            cancellationToken);

        var report = new MedicalReport
        {
            PatientId = request.PatientId,
            UploadedByUserId = request.UploadedByUserId,
            ReportType = request.ReportType,
            OriginalFileName = Path.GetFileName(request.OriginalFileName),
            StoredFileName = upload.StoredFileName,
            ContentType = request.ContentType,
            FileExtension = Path.GetExtension(request.OriginalFileName).ToLowerInvariant(),
            FileSizeBytes = request.FileSizeBytes,
            BlobContainer = upload.ContainerName,
            BlobName = upload.BlobName,
            PatientNotes = request.PatientNotes,
            UploadedAt = DateTime.UtcNow,
            Status = "Uploaded"
        };

        try
        {
            await _reportRepository.AddAsync(report);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception persistenceError)
        {
            // Storage and SQL cannot share a transaction. Compensate when metadata
            // is not committed; preserve the original failure if cleanup fails.
            try { await _blobStorageService.DeleteAsync(upload.ContainerName, upload.BlobName, CancellationToken.None); }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Report persistence and storage cleanup failed", persistenceError, cleanupError);
            }
            throw;
        }

        return MedicalReportHandlerHelpers.ToDto(report);
    }
}

public class AddMedicalReportNotesCommandHandler : IRequestHandler<AddMedicalReportNotesCommand, MedicalReportDto>
{
    private readonly IRepository<MedicalReportNote> _notes;
    private readonly ICurrentUserService _user;
    private readonly IRepository<MedicalReport> _reportRepository;
    private readonly IClinicalAccessService _access;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<DietPlan> _dietPlanRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddMedicalReportNotesCommandHandler(
        IRepository<MedicalReport> reportRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<DietPlan> dietPlanRepository,
        IUnitOfWork unitOfWork, IClinicalAccessService access, IRepository<MedicalReportNote> notes, ICurrentUserService user)
    {
        _reportRepository = reportRepository;
        _access = access;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _appointmentRepository = appointmentRepository;
        _dietPlanRepository = dietPlanRepository;
        _unitOfWork = unitOfWork;
        _notes = notes;
        _user = user;
    }

    public async Task<MedicalReportDto> Handle(AddMedicalReportNotesCommand request, CancellationToken cancellationToken)
    {
        var report = await MedicalReportHandlerHelpers.GetRequiredAsync(_reportRepository, request.ReportId);
        await _access.EnsureAssignedClinicianAsync(report.PatientId, cancellationToken);

        if (_user.Role != "Nutritionist")
        {
            throw new ForbiddenAccessException("Only the assigned nutritionist can add clinical notes");
        }

        report.NutritionistNotes = request.Notes;
        report.ReviewedByUserId = _user.UserId;
        report.ReviewedAt = DateTime.UtcNow;
        report.Status = "Reviewed";
        var previous = (await _notes.GetAsync(q => q.Where(n => n.MedicalReportId == report.Id && n.DeletedAt == null).OrderByDescending(n => n.CreatedAt))).FirstOrDefault();
        await _notes.AddAsync(new MedicalReportNote { MedicalReportId = report.Id, AuthorUserId = _user.UserId,
            Content = request.Notes, SupersedesId = previous?.Id });

        await _reportRepository.UpdateAsync(report);
        await _unitOfWork.SaveChangesAsync();

        return MedicalReportHandlerHelpers.ToDto(report);
    }
}

public class GetMedicalReportsQueryHandler : IRequestHandler<GetMedicalReportsQuery, IEnumerable<MedicalReportDto>>
{
    private readonly IRepository<MedicalReport> _reportRepository;
    private readonly IClinicalAccessService _access;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<DietPlan> _dietPlanRepository;

    public GetMedicalReportsQueryHandler(
        IRepository<MedicalReport> reportRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<DietPlan> dietPlanRepository, IClinicalAccessService access)
    {
        _reportRepository = reportRepository;
        _access = access;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _appointmentRepository = appointmentRepository;
        _dietPlanRepository = dietPlanRepository;
    }

    public async Task<IEnumerable<MedicalReportDto>> Handle(GetMedicalReportsQuery request, CancellationToken cancellationToken)
    {
        await _access.EnsurePatientAccessAsync(request.PatientId, cancellationToken: cancellationToken);

        var reports = await _reportRepository.GetAsync(query =>
        {
            var filtered = query.Where(r => r.PatientId == request.PatientId && r.DeletedAt == null);
            if (!string.IsNullOrWhiteSpace(request.ReportType))
            {
                filtered = filtered.Where(r => r.ReportType == request.ReportType);
            }

            return filtered.OrderByDescending(r => r.UploadedAt);
        });

        return reports.Select(MedicalReportHandlerHelpers.ToDto).ToArray();
    }
}

public class GetMedicalReportByIdQueryHandler : IRequestHandler<GetMedicalReportByIdQuery, MedicalReportDto>
{
    private readonly IRepository<MedicalReport> _reportRepository;
    private readonly IClinicalAccessService _access;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<DietPlan> _dietPlanRepository;

    public GetMedicalReportByIdQueryHandler(
        IRepository<MedicalReport> reportRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<DietPlan> dietPlanRepository, IClinicalAccessService access)
    {
        _reportRepository = reportRepository;
        _access = access;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _appointmentRepository = appointmentRepository;
        _dietPlanRepository = dietPlanRepository;
    }

    public async Task<MedicalReportDto> Handle(GetMedicalReportByIdQuery request, CancellationToken cancellationToken)
    {
        var report = await MedicalReportHandlerHelpers.GetRequiredAsync(_reportRepository, request.ReportId);
        await _access.EnsurePatientAccessAsync(report.PatientId);

        return MedicalReportHandlerHelpers.ToDto(report);
    }
}

public class DownloadMedicalReportQueryHandler : IRequestHandler<DownloadMedicalReportQuery, MedicalReportFileDto>
{
    private readonly IRepository<MedicalReport> _reportRepository;
    private readonly IClinicalAccessService _access;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<DietPlan> _dietPlanRepository;
    private readonly IBlobStorageService _blobStorageService;

    public DownloadMedicalReportQueryHandler(
        IRepository<MedicalReport> reportRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<DietPlan> dietPlanRepository,
        IBlobStorageService blobStorageService, IClinicalAccessService access)
    {
        _reportRepository = reportRepository;
        _access = access;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _appointmentRepository = appointmentRepository;
        _dietPlanRepository = dietPlanRepository;
        _blobStorageService = blobStorageService;
    }

    public async Task<MedicalReportFileDto> Handle(DownloadMedicalReportQuery request, CancellationToken cancellationToken)
    {
        var report = await MedicalReportHandlerHelpers.GetRequiredAsync(_reportRepository, request.ReportId);
        await _access.EnsurePatientAccessAsync(report.PatientId);

        var file = await _blobStorageService.DownloadAsync(report.BlobContainer, report.BlobName, report.ContentType, cancellationToken);
        return new MedicalReportFileDto
        {
            FileName = report.OriginalFileName,
            ContentType = file.ContentType,
            Content = file.Content
        };
    }
}

internal static class MedicalReportHandlerHelpers
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

    internal static MedicalReportDto ToDto(MedicalReport report)
    {
        return new MedicalReportDto
        {
            Id = report.Id,
            PatientId = report.PatientId,
            UploadedByUserId = report.UploadedByUserId,
            ReportType = report.ReportType,
            OriginalFileName = report.OriginalFileName,
            ContentType = report.ContentType,
            FileExtension = report.FileExtension,
            FileSizeBytes = report.FileSizeBytes,
            PatientNotes = report.PatientNotes,
            NutritionistNotes = report.NutritionistNotes,
            ReviewedByUserId = report.ReviewedByUserId,
            ReviewedAt = report.ReviewedAt,
            UploadedAt = report.UploadedAt,
            Status = report.Status,
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt
        };
    }
}
