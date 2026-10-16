namespace NutriCare.API.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriCare.Application.Commands.Progress;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Progress;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProgressController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IClinicalAccessService _clinicalAccess;

    public ProgressController(IMediator mediator, IRepository<Patient> patientRepository, IClinicalAccessService clinicalAccess)
    {
        _mediator = mediator;
        _patientRepository = patientRepository;
        _clinicalAccess = clinicalAccess;
    }

    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<ProgressRecordDto>>> TrackProgress([FromBody] UpsertProgressRecordRequest request)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new TrackProgressCommand
        {
            PatientId = patient.Id,
            RecordDate = request.RecordDate,
            Weight = request.Weight,
            BMI = request.BMI,
            BodyFatPercentage = request.BodyFatPercentage,
            WaterIntakeLiters = request.WaterIntakeLiters,
            DailyCalories = request.DailyCalories,
            ExerciseMinutes = request.ExerciseMinutes,
            ExerciseType = request.ExerciseType,
            Notes = request.Notes,
            WaistCircumference = request.WaistCircumference,
            ChestCircumference = request.ChestCircumference,
            HipCircumference = request.HipCircumference,
            ArmCircumference = request.ArmCircumference,
            ThighCircumference = request.ThighCircumference,
            ExpectedVersion = request.ExpectedVersion,
            ChangeReason = request.ChangeReason
        });

        return Ok(ApiResponse<ProgressRecordDto>.SuccessResponse(result, "Progress tracked successfully"));
    }

    [HttpGet("history")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProgressRecordDto>>>> GetMyHistory(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new GetProgressHistoryQuery
        {
            PatientId = patient.Id,
            FromDate = fromDate,
            ToDate = toDate
        });

        return Ok(ApiResponse<IEnumerable<ProgressRecordDto>>.SuccessResponse(result));
    }

    [HttpGet("patient/{patientId:long}/history")]
    [Authorize(Roles = "Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProgressRecordDto>>>> GetPatientHistory(
        long patientId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        await _clinicalAccess.EnsurePatientAccessAsync(patientId, allowPatient: false);
        var result = await _mediator.Send(new GetProgressHistoryQuery
        {
            PatientId = patientId,
            FromDate = fromDate,
            ToDate = toDate
        });

        return Ok(ApiResponse<IEnumerable<ProgressRecordDto>>.SuccessResponse(result));
    }

    [HttpGet("charts")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProgressChartPointDto>>>> GetMyChartData(
        [FromQuery] string metric = "weight",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new GetProgressChartDataQuery
        {
            PatientId = patient.Id,
            Metric = metric,
            FromDate = fromDate,
            ToDate = toDate
        });

        return Ok(ApiResponse<IEnumerable<ProgressChartPointDto>>.SuccessResponse(result));
    }

    [HttpGet("patient/{patientId:long}/charts")]
    [Authorize(Roles = "Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProgressChartPointDto>>>> GetPatientChartData(
        long patientId,
        [FromQuery] string metric = "weight",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        await _clinicalAccess.EnsurePatientAccessAsync(patientId, allowPatient: false);
        var result = await _mediator.Send(new GetProgressChartDataQuery
        {
            PatientId = patientId,
            Metric = metric,
            FromDate = fromDate,
            ToDate = toDate
        });

        return Ok(ApiResponse<IEnumerable<ProgressChartPointDto>>.SuccessResponse(result));
    }

    [HttpGet("analytics")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<ProgressAnalyticsDto>>> GetMyAnalytics(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new GetProgressAnalyticsQuery
        {
            PatientId = patient.Id,
            FromDate = fromDate,
            ToDate = toDate
        });

        return Ok(ApiResponse<ProgressAnalyticsDto>.SuccessResponse(result));
    }

    [HttpGet("patient/{patientId:long}/analytics")]
    [Authorize(Roles = "Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<ProgressAnalyticsDto>>> GetPatientAnalytics(
        long patientId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        await _clinicalAccess.EnsurePatientAccessAsync(patientId, allowPatient: false);
        var result = await _mediator.Send(new GetProgressAnalyticsQuery
        {
            PatientId = patientId,
            FromDate = fromDate,
            ToDate = toDate
        });

        return Ok(ApiResponse<ProgressAnalyticsDto>.SuccessResponse(result));
    }

    private async Task<Patient> GetCurrentPatientAsync()
    {
        var userId = GetCurrentUserId();
        var patients = await _patientRepository.GetAsync(query => query.Where(p => p.UserId == userId && p.DeletedAt == null));
        return patients.FirstOrDefault()
            ?? throw new InvalidOperationException("Patient profile was not found for the current user");
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : 0;
    }
}
