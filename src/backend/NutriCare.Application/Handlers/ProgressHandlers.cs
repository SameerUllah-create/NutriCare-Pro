namespace NutriCare.Application.Handlers.Progress;

using MediatR;
using System.Text.Json;
using NutriCare.Application.Commands.Progress;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Progress;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public class TrackProgressCommandHandler : IRequestHandler<TrackProgressCommand, ProgressRecordDto>
{
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<WeightTracking> _progressRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<ProgressRevision> _revisions;
    private readonly IClinicalAccessService _access;
    private readonly ICurrentUserService _user;
    private readonly IAppointmentReservationLock _reservationLock;
    private readonly IClinicalConsentService _consent;

    public TrackProgressCommandHandler(
        IRepository<Patient> patientRepository,
        IRepository<WeightTracking> progressRepository,
        IUnitOfWork unitOfWork,
        IRepository<ProgressRevision> revisions,
        IClinicalAccessService access,
        ICurrentUserService user,
        IAppointmentReservationLock reservationLock, IClinicalConsentService consent)
    {
        _patientRepository = patientRepository;
        _progressRepository = progressRepository;
        _unitOfWork = unitOfWork;
        _revisions = revisions;
        _access = access;
        _user = user;
        _reservationLock = reservationLock;
        _consent = consent;
    }

    public async Task<ProgressRecordDto> Handle(TrackProgressCommand request, CancellationToken cancellationToken)
    {
        if (request.PatientId != await _access.GetPatientIdAsync(cancellationToken))
            throw new ForbiddenAccessException("You can only record your own progress");
        await _consent.RequireAsync(_user.UserId, "HealthData", cancellationToken: cancellationToken);
        return await _reservationLock.ExecuteAsync(-request.PatientId, async () =>
        {
        var patient = await ProgressHandlerHelpers.GetRequiredAsync(_patientRepository, request.PatientId);
        var recordDate = request.RecordDate.Date;
        var existingRecords = await _progressRepository.GetAsync(query => query.Where(w =>
            w.PatientId == request.PatientId &&
            w.RecordDate == recordDate &&
            w.DeletedAt == null));

        var progress = existingRecords.FirstOrDefault() ?? new WeightTracking
        {
            PatientId = request.PatientId,
            RecordDate = recordDate
        };

        if (progress.Id != 0)
        {
            if (request.ExpectedVersion == null || !request.ExpectedVersion.SequenceEqual(progress.RowVersion))
                throw new ConflictException("This measurement changed. Reload its current version before correcting it");
            if (string.IsNullOrWhiteSpace(request.ChangeReason)) throw new ArgumentException("Explain why the measurement is being corrected");
            await _revisions.AddAsync(new ProgressRevision { WeightTrackingId = progress.Id,
                SnapshotJson = JsonSerializer.Serialize(ProgressHandlerHelpers.ToDto(progress)), ChangedByUserId = _user.UserId, Reason = request.ChangeReason });
        }

        progress.Weight = request.Weight;
        progress.BMI = request.BMI ?? ProgressHandlerHelpers.CalculateBMI(request.Weight, patient.Height);
        progress.BMIIsCalculated = !request.BMI.HasValue && progress.BMI.HasValue;
        progress.BodyFatPercentage = request.BodyFatPercentage;
        progress.WaterIntakeLiters = request.WaterIntakeLiters;
        progress.DailyCalories = request.DailyCalories;
        progress.ExerciseMinutes = request.ExerciseMinutes;
        progress.ExerciseType = request.ExerciseType;
        progress.Notes = request.Notes;
        progress.WaistCircumference = request.WaistCircumference;
        progress.ChestCircumference = request.ChestCircumference;
        progress.HipCircumference = request.HipCircumference;
        progress.ArmCircumference = request.ArmCircumference;
        progress.ThighCircumference = request.ThighCircumference;
        var measurements = new[] { request.WaistCircumference, request.ChestCircumference, request.HipCircumference, request.ArmCircumference, request.ThighCircumference };
        if (measurements.Any(m => m.HasValue && (m <= 0 || m > 400))) throw new ArgumentException("Measurements must be greater than zero and at most 400 cm");

        // A backdated correction must not replace the latest measured weight.
        var moreRecent = await _progressRepository.GetAsync(query => query.Where(w =>
            w.PatientId == request.PatientId && w.RecordDate > recordDate && w.DeletedAt == null));
        if (!moreRecent.Any())
        {
            patient.CurrentWeight = request.Weight;
            await _patientRepository.UpdateAsync(patient);
        }

        if (progress.Id == 0)
        {
            await _progressRepository.AddAsync(progress);
        }
        else
        {
            await _progressRepository.UpdateAsync(progress);
        }

        await _unitOfWork.SaveChangesAsync();
        return ProgressHandlerHelpers.ToDto(progress);
        }, cancellationToken);
    }
}

public class GetProgressHistoryQueryHandler : IRequestHandler<GetProgressHistoryQuery, IEnumerable<ProgressRecordDto>>
{
    private readonly IRepository<WeightTracking> _progressRepository;

    public GetProgressHistoryQueryHandler(IRepository<WeightTracking> progressRepository)
    {
        _progressRepository = progressRepository;
    }

    public async Task<IEnumerable<ProgressRecordDto>> Handle(GetProgressHistoryQuery request, CancellationToken cancellationToken)
    {
        var records = await ProgressHandlerHelpers.GetRecordsAsync(_progressRepository, request.PatientId, request.FromDate, request.ToDate);
        return records.OrderByDescending(r => r.RecordDate).Select(ProgressHandlerHelpers.ToDto).ToArray();
    }
}

public class GetProgressChartDataQueryHandler : IRequestHandler<GetProgressChartDataQuery, IEnumerable<ProgressChartPointDto>>
{
    private readonly IRepository<WeightTracking> _progressRepository;

    public GetProgressChartDataQueryHandler(IRepository<WeightTracking> progressRepository)
    {
        _progressRepository = progressRepository;
    }

    public async Task<IEnumerable<ProgressChartPointDto>> Handle(GetProgressChartDataQuery request, CancellationToken cancellationToken)
    {
        var metric = ProgressHandlerHelpers.NormalizeMetric(request.Metric);
        var records = await ProgressHandlerHelpers.GetRecordsAsync(_progressRepository, request.PatientId, request.FromDate, request.ToDate);

        return records
            .OrderBy(r => r.RecordDate)
            .Select(r => new ProgressChartPointDto
            {
                Date = r.RecordDate,
                Metric = metric,
                Value = ProgressHandlerHelpers.GetMetricValue(r, metric)
            })
            .Where(p => p.Value >= 0)
            .ToArray();
    }
}

public class GetProgressAnalyticsQueryHandler : IRequestHandler<GetProgressAnalyticsQuery, ProgressAnalyticsDto>
{
    private readonly IRepository<WeightTracking> _progressRepository;

    public GetProgressAnalyticsQueryHandler(IRepository<WeightTracking> progressRepository)
    {
        _progressRepository = progressRepository;
    }

    public async Task<ProgressAnalyticsDto> Handle(GetProgressAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var records = (await ProgressHandlerHelpers.GetRecordsAsync(_progressRepository, request.PatientId, request.FromDate, request.ToDate))
            .OrderBy(r => r.RecordDate)
            .ToArray();

        var startWeight = records.FirstOrDefault()?.Weight;
        var currentWeight = records.LastOrDefault()?.Weight;

        return new ProgressAnalyticsDto
        {
            FromDate = records.FirstOrDefault()?.RecordDate,
            ToDate = records.LastOrDefault()?.RecordDate,
            RecordCount = records.Length,
            StartWeight = startWeight,
            CurrentWeight = currentWeight,
            WeightChange = startWeight.HasValue && currentWeight.HasValue ? currentWeight.Value - startWeight.Value : null,
            AverageBMI = ProgressHandlerHelpers.Average(records.Select(r => r.BMI)),
            AverageBodyFatPercentage = ProgressHandlerHelpers.Average(records.Select(r => r.BodyFatPercentage)),
            AverageWaterIntakeLiters = ProgressHandlerHelpers.Average(records.Select(r => r.WaterIntakeLiters)),
            AverageDailyCalories = ProgressHandlerHelpers.Average(records.Select(r => r.DailyCalories.HasValue ? (decimal?)r.DailyCalories.Value : null)),
            TotalExerciseMinutes = records.Sum(r => r.ExerciseMinutes ?? 0)
        };
    }
}

internal static class ProgressHandlerHelpers
{
    private static readonly string[] Metrics =
    {
        "weight",
        "bmi",
        "bodyFatPercentage",
        "waterIntake",
        "dailyCalories",
        "exerciseMinutes"
        ,"waistCircumference", "chestCircumference", "hipCircumference", "armCircumference", "thighCircumference"
    };

    internal static async Task<T> GetRequiredAsync<T>(IRepository<T> repository, long id) where T : BaseEntity
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted)
        {
            throw new EntityNotFoundException(typeof(T).Name, id);
        }

        return entity;
    }

    internal static async Task<IReadOnlyCollection<WeightTracking>> GetRecordsAsync(
        IRepository<WeightTracking> progressRepository,
        long patientId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var records = await progressRepository.GetAsync(query =>
        {
            var filtered = query.Where(w => w.PatientId == patientId && w.DeletedAt == null);
            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                filtered = filtered.Where(w => w.RecordDate >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date;
                filtered = filtered.Where(w => w.RecordDate <= to);
            }

            return filtered;
        });

        return records.ToArray();
    }

    internal static decimal? CalculateBMI(decimal weight, decimal heightCm)
    {
        if (heightCm <= 0)
        {
            return null;
        }

        var heightMeters = heightCm / 100;
        return Math.Round(weight / (heightMeters * heightMeters), 2);
    }

    internal static string NormalizeMetric(string metric)
    {
        var normalized = Metrics.FirstOrDefault(m => string.Equals(m, metric, StringComparison.OrdinalIgnoreCase));
        if (normalized == null)
        {
            throw new ArgumentException("Invalid progress metric");
        }

        return normalized;
    }

    internal static decimal GetMetricValue(WeightTracking record, string metric)
    {
        return metric switch
        {
            "weight" => record.Weight,
            "bmi" => record.BMI ?? -1,
            "bodyFatPercentage" => record.BodyFatPercentage ?? -1,
            "waterIntake" => record.WaterIntakeLiters ?? -1,
            "dailyCalories" => record.DailyCalories ?? -1,
            "exerciseMinutes" => record.ExerciseMinutes ?? -1,
            "waistCircumference" => record.WaistCircumference ?? -1,
            "chestCircumference" => record.ChestCircumference ?? -1,
            "hipCircumference" => record.HipCircumference ?? -1,
            "armCircumference" => record.ArmCircumference ?? -1,
            "thighCircumference" => record.ThighCircumference ?? -1,
            _ => -1
        };
    }

    internal static decimal? Average(IEnumerable<decimal?> values)
    {
        var availableValues = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return availableValues.Length == 0 ? null : Math.Round(availableValues.Average(), 2);
    }

    internal static ProgressRecordDto ToDto(WeightTracking progress)
    {
        return new ProgressRecordDto
        {
            Id = progress.Id,
            PatientId = progress.PatientId,
            RecordDate = progress.RecordDate,
            Weight = progress.Weight,
            BMI = progress.BMI,
            BMIIsCalculated = progress.BMIIsCalculated,
            WaistCircumference = progress.WaistCircumference,
            ChestCircumference = progress.ChestCircumference,
            HipCircumference = progress.HipCircumference,
            ArmCircumference = progress.ArmCircumference,
            ThighCircumference = progress.ThighCircumference,
            RowVersion = progress.RowVersion,
            BodyFatPercentage = progress.BodyFatPercentage,
            WaterIntakeLiters = progress.WaterIntakeLiters,
            DailyCalories = progress.DailyCalories,
            ExerciseMinutes = progress.ExerciseMinutes,
            ExerciseType = progress.ExerciseType,
            Notes = progress.Notes,
            CreatedAt = progress.CreatedAt,
            UpdatedAt = progress.UpdatedAt
        };
    }
}
