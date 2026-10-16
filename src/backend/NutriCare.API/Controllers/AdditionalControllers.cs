namespace NutriCare.API.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriCare.Application.Commands.DietPlans;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.DietPlans;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Diet Plans controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DietPlansController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly NutriCareDbContext _context;
    private readonly INotificationService _notificationService;

    public DietPlansController(
        IMediator mediator,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository,
        NutriCareDbContext context,
        INotificationService notificationService)
    {
        _mediator = mediator;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _context = context;
        _notificationService = notificationService;
    }

    [HttpPost]
    [Authorize(Roles = "Nutritionist")]
    public async Task<ActionResult<ApiResponse<DietPlanDto>>> CreateDietPlan([FromBody] UpsertDietPlanRequest request)
    {
        var nutritionist = await GetCurrentNutritionistAsync();
        var result = await _mediator.Send(new CreateDietPlanCommand
        {
            NutritionistId = nutritionist.Id,
            PatientId = request.PatientId,
            Title = request.Title,
            Description = request.Description,
            Objective = request.Objective,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ReviewDate = request.ReviewDate,
            ChangeReason = request.ChangeReason,
            DailyCalories = request.DailyCalories,
            ProteinGrams = request.ProteinGrams,
            CarbsGrams = request.CarbsGrams,
            FatsGrams = request.FatsGrams,
            FiberGrams = request.FiberGrams,
            WaterIntakeLiters = request.WaterIntakeLiters,
            Supplements = request.Supplements,
            Instructions = request.Instructions,
            Notes = request.Notes,
            Meals = request.Meals
        });

        return CreatedAtAction(nameof(GetDietPlan), new { id = result.Id },
            ApiResponse<DietPlanDto>.SuccessResponse(result, "Diet plan created successfully", 201));
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<ActionResult<ApiResponse<DietPlanDto>>> UpdateDietPlan(long id, [FromBody] UpsertDietPlanRequest request)
    {
        var nutritionist = await GetCurrentNutritionistAsync();
        var result = await _mediator.Send(new UpdateDietPlanCommand
        {
            DietPlanId = id,
            NutritionistId = nutritionist.Id,
            ReviewDate = request.ReviewDate,
            ChangeReason = request.ChangeReason,
            ExpectedVersion = request.ExpectedVersion,
            PatientId = request.PatientId,
            Title = request.Title,
            Description = request.Description,
            Objective = request.Objective,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DailyCalories = request.DailyCalories,
            ProteinGrams = request.ProteinGrams,
            CarbsGrams = request.CarbsGrams,
            FatsGrams = request.FatsGrams,
            FiberGrams = request.FiberGrams,
            WaterIntakeLiters = request.WaterIntakeLiters,
            Supplements = request.Supplements,
            Instructions = request.Instructions,
            Notes = request.Notes,
            Meals = request.Meals
        });

        return Ok(ApiResponse<DietPlanDto>.SuccessResponse(result, "Diet plan updated successfully"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Patient,Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<DietPlanDto>>> GetDietPlan(long id)
    {
        var result = await _mediator.Send(new GetDietPlanByIdQuery
        {
            DietPlanId = id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole()
        });

        return Ok(ApiResponse<DietPlanDto>.SuccessResponse(result));
    }

    [HttpGet("my-plans")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<IEnumerable<DietPlanDto>>>> GetMyAssignedPlans([FromQuery] string? status = null)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new GetPatientDietPlansQuery
        {
            PatientId = patient.Id,
            Status = status
        });

        return Ok(ApiResponse<IEnumerable<DietPlanDto>>.SuccessResponse(result));
    }

    [HttpGet("patient/{patientId:long}")]
    [Authorize(Roles = "Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<DietPlanDto>>>> GetPatientDietPlans(long patientId, [FromQuery] string? status = null)
    {
        var result = await _mediator.Send(new GetPatientDietPlansQuery
        {
            PatientId = patientId,
            Status = status
        });

        return Ok(ApiResponse<IEnumerable<DietPlanDto>>.SuccessResponse(result));
    }

    [HttpGet("nutritionist")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<ActionResult<ApiResponse<IEnumerable<DietPlanDto>>>> GetMyNutritionistPlans(
        [FromQuery] long? patientId = null,
        [FromQuery] string? status = null)
    {
        var nutritionist = await GetCurrentNutritionistAsync();
        var result = await _mediator.Send(new GetNutritionistDietPlansQuery
        {
            NutritionistId = nutritionist.Id,
            PatientId = patientId,
            Status = status
        });

        return Ok(ApiResponse<IEnumerable<DietPlanDto>>.SuccessResponse(result));
    }

    [HttpGet("{id:long}/download")]
    [Authorize(Roles = "Patient,Nutritionist,Admin")]
    public async Task<IActionResult> DownloadPdf(long id)
    {
        var result = await _mediator.Send(new DownloadDietPlanPdfQuery
        {
            DietPlanId = id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole()
        });

        return File(result.Content, "application/pdf", result.FileName);
    }

    [HttpPost("{id:long}/compliance")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<DietPlanComplianceDto>>> TrackCompliance(long id, [FromBody] TrackDietPlanComplianceRequest request)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new TrackDietPlanComplianceCommand
        {
            DietPlanId = id,
            PatientId = patient.Id,
            RecordDate = request.RecordDate,
            MealsCompleted = request.MealsCompleted,
            TotalMeals = request.TotalMeals,
            WaterIntakeLiters = request.WaterIntakeLiters,
            TookSupplements = request.TookSupplements,
            Notes = request.Notes
        });

        return Ok(ApiResponse<DietPlanComplianceDto>.SuccessResponse(result, "Compliance tracked successfully"));
    }

    [HttpGet("{id:long}/compliance")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<IEnumerable<DietPlanComplianceDto>>>> GetCompliance(long id)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new GetDietPlanComplianceQuery
        {
            DietPlanId = id,
            PatientId = patient.Id
        });

        return Ok(ApiResponse<IEnumerable<DietPlanComplianceDto>>.SuccessResponse(result));
    }

    private async Task<Patient> GetCurrentPatientAsync()
    {
        var userId = GetCurrentUserId();
        var patients = await _patientRepository.GetAsync(query => query.Where(p => p.UserId == userId && p.DeletedAt == null));
        return patients.FirstOrDefault()
            ?? throw new InvalidOperationException("Patient profile was not found for the current user");
    }

    private async Task<Nutritionist> GetCurrentNutritionistAsync()
    {
        var userId = GetCurrentUserId();
        var nutritionists = await _nutritionistRepository.GetAsync(query => query.Where(n => n.UserId == userId && n.DeletedAt == null));
        return nutritionists.FirstOrDefault()
            ?? throw new InvalidOperationException("Nutritionist profile was not found for the current user");
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : 0;
    }

    private string GetCurrentRole()
    {
        return User.FindFirst("role")?.Value ?? string.Empty;
    }

    private async Task NotifyDietPlanAssignedAsync(DietPlanDto dietPlan)
    {
        var patient = await _context.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == dietPlan.PatientId);

        var recipient = ToRecipient(patient?.User);
        if (recipient == null)
        {
            return;
        }

        await _notificationService.SendDietPlanAssignedAsync(recipient, dietPlan.Id, dietPlan.Title);
    }

    private static NotificationRecipient? ToRecipient(User? user)
    {
        return user == null
            ? null
            : new NotificationRecipient(
                user.Id,
                user.Email ?? string.Empty,
                user.PhoneNumber,
                $"{user.FirstName} {user.LastName}".Trim());
    }
}

