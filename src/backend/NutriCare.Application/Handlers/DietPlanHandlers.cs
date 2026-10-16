namespace NutriCare.Application.Handlers.DietPlans;


using NutriCare.Application.Services;
using MediatR;
using NutriCare.Application.Commands.DietPlans;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.DietPlans;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public sealed class CreateDietPlanCommandHandler(ClinicalPlanWorkflow workflow) : IRequestHandler<CreateDietPlanCommand, DietPlanDto>
{
    public Task<DietPlanDto> Handle(CreateDietPlanCommand request, CancellationToken cancellationToken) => workflow.CreateAsync(request, cancellationToken);
}

public sealed class UpdateDietPlanCommandHandler(ClinicalPlanWorkflow workflow) : IRequestHandler<UpdateDietPlanCommand, DietPlanDto>
{
    public Task<DietPlanDto> Handle(UpdateDietPlanCommand request, CancellationToken cancellationToken) => workflow.UpdateAsync(request, cancellationToken);
}
public class TrackDietPlanComplianceCommandHandler : IRequestHandler<TrackDietPlanComplianceCommand, DietPlanComplianceDto>
{
    private readonly IRepository<DietPlan> _dietPlanRepository;
    private readonly IRepository<DietPlanCompliance> _complianceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ClinicalPlanWorkflow _workflow;

    public TrackDietPlanComplianceCommandHandler(
        IRepository<DietPlan> dietPlanRepository,
        IRepository<DietPlanCompliance> complianceRepository,
        IUnitOfWork unitOfWork, ClinicalPlanWorkflow workflow)
    {
        _dietPlanRepository = dietPlanRepository;
        _complianceRepository = complianceRepository;
        _unitOfWork = unitOfWork;
        _workflow = workflow;
    }

    public async Task<DietPlanComplianceDto> Handle(TrackDietPlanComplianceCommand request, CancellationToken cancellationToken)
    {
        var visible = await _workflow.VisibleAsync(request.DietPlanId, cancellationToken);
        var plan = await DietPlanHandlerHelpers.GetRequiredAsync(_dietPlanRepository, request.DietPlanId);
        if (plan.PatientId != request.PatientId)
        {
            throw new ForbiddenAccessException("You can only track compliance for your assigned diet plans");
        }

        var recordDate = request.RecordDate.Date;
        if (visible.PublishedAt == null || recordDate < visible.StartDate.Date || recordDate > visible.EndDate.Date)
            throw new ArgumentException("Adherence must refer to a date in a published plan");
        var existing = await _complianceRepository.GetAsync(query => query.Where(c =>
            c.DietPlanId == request.DietPlanId &&
            c.PatientId == request.PatientId &&
            c.RecordDate == recordDate &&
            c.DeletedAt == null));

        var compliance = existing.FirstOrDefault() ?? new DietPlanCompliance
        {
            DietPlanId = request.DietPlanId,
            PatientId = request.PatientId,
            RecordDate = recordDate
        };

        compliance.MealsCompleted = request.MealsCompleted;
        compliance.TotalMeals = request.TotalMeals;
        compliance.WaterIntakeLiters = request.WaterIntakeLiters;
        compliance.TookSupplements = request.TookSupplements;
        compliance.Notes = request.Notes;

        if (compliance.Id == 0)
        {
            await _complianceRepository.AddAsync(compliance);
        }
        else
        {
            await _complianceRepository.UpdateAsync(compliance);
        }

        await _unitOfWork.SaveChangesAsync();
        return DietPlanHandlerHelpers.ToComplianceDto(compliance);
    }
}

public sealed class GetDietPlanByIdQueryHandler(ClinicalPlanWorkflow workflow) : IRequestHandler<GetDietPlanByIdQuery, DietPlanDto>
{
    public Task<DietPlanDto> Handle(GetDietPlanByIdQuery request, CancellationToken cancellationToken) => workflow.VisibleAsync(request.DietPlanId, cancellationToken);
}

public sealed class GetPatientDietPlansQueryHandler(ClinicalPlanWorkflow workflow) : IRequestHandler<GetPatientDietPlansQuery, IEnumerable<DietPlanDto>>
{
    public async Task<IEnumerable<DietPlanDto>> Handle(GetPatientDietPlansQuery request, CancellationToken cancellationToken) => await workflow.PatientPlansAsync(request.PatientId, request.Status, cancellationToken);
}
public class GetNutritionistDietPlansQueryHandler : IRequestHandler<GetNutritionistDietPlansQuery, IEnumerable<DietPlanDto>>
{
    private readonly IRepository<DietPlan> _dietPlanRepository;
    private readonly IRepository<DietPlanItem> _itemRepository;
    private readonly IClinicalAccessService _access;

    public GetNutritionistDietPlansQueryHandler(IRepository<DietPlan> dietPlanRepository, IRepository<DietPlanItem> itemRepository, IClinicalAccessService access)
    {
        _dietPlanRepository = dietPlanRepository;
        _itemRepository = itemRepository;
        _access = access;
    }

    public async Task<IEnumerable<DietPlanDto>> Handle(GetNutritionistDietPlansQuery request, CancellationToken cancellationToken)
    {
        if (request.NutritionistId != await _access.GetNutritionistIdAsync(cancellationToken))
            throw new ForbiddenAccessException("You can only view your own plans");
        if (request.PatientId.HasValue) await _access.EnsureAssignedClinicianAsync(request.PatientId.Value, cancellationToken);
        var plans = await _dietPlanRepository.GetAsync(query =>
        {
            var filtered = query.Where(d => d.NutritionistId == request.NutritionistId && d.DeletedAt == null);
            if (request.PatientId.HasValue)
            {
                filtered = filtered.Where(d => d.PatientId == request.PatientId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                filtered = filtered.Where(d => d.Status == request.Status);
            }

            return filtered.OrderByDescending(d => d.StartDate);
        });

        var permitted = new List<DietPlan>();
        foreach (var plan in plans)
            if (await _access.CanAccessPatientAsync(plan.PatientId, cancellationToken)) permitted.Add(plan);
        return await DietPlanHandlerHelpers.ToDtosAsync(_itemRepository, permitted);
    }
}

public sealed class DownloadDietPlanPdfQueryHandler(ClinicalPlanWorkflow workflow, IClinicalPdfRenderer renderer) : IRequestHandler<DownloadDietPlanPdfQuery, DietPlanPdfDto>
{
    public async Task<DietPlanPdfDto> Handle(DownloadDietPlanPdfQuery request, CancellationToken cancellationToken)
    {
        var dto = await workflow.VisibleAsync(request.DietPlanId, cancellationToken);
        return new DietPlanPdfDto { FileName = $"diet-plan-{dto.Id}-v{dto.Version}.pdf", Content = await renderer.RenderAsync(ClinicalPlanHtml.Render(dto), cancellationToken) };
    }
}
public class GetDietPlanComplianceQueryHandler : IRequestHandler<GetDietPlanComplianceQuery, IEnumerable<DietPlanComplianceDto>>
{
    private readonly IRepository<DietPlan> _dietPlanRepository;
    private readonly IRepository<DietPlanCompliance> _complianceRepository;

    public GetDietPlanComplianceQueryHandler(
        IRepository<DietPlan> dietPlanRepository,
        IRepository<DietPlanCompliance> complianceRepository)
    {
        _dietPlanRepository = dietPlanRepository;
        _complianceRepository = complianceRepository;
    }

    public async Task<IEnumerable<DietPlanComplianceDto>> Handle(GetDietPlanComplianceQuery request, CancellationToken cancellationToken)
    {
        var plan = await DietPlanHandlerHelpers.GetRequiredAsync(_dietPlanRepository, request.DietPlanId);
        if (plan.PatientId != request.PatientId)
        {
            throw new ForbiddenAccessException("You can only view compliance for your assigned diet plans");
        }

        var records = await _complianceRepository.GetAsync(query => query
            .Where(c => c.DietPlanId == request.DietPlanId && c.PatientId == request.PatientId && c.DeletedAt == null)
            .OrderByDescending(c => c.RecordDate));

        return records.Select(DietPlanHandlerHelpers.ToComplianceDto);
    }
}

internal static class DietPlanHandlerHelpers
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

    internal static void ApplyPlanFields(DietPlan plan, CreateDietPlanCommand request)
    {
        plan.NutritionistId = request.NutritionistId;
        plan.PatientId = request.PatientId;
        ApplyCommonPlanFields(plan, request.Title, request.Description, request.Objective, request.StartDate, request.EndDate, request.DailyCalories, request.ProteinGrams, request.CarbsGrams, request.FatsGrams, request.FiberGrams, request.WaterIntakeLiters, request.Supplements, request.Instructions, request.Notes, request.Meals);
    }

    internal static void ApplyPlanFields(DietPlan plan, UpdateDietPlanCommand request)
    {
        plan.PatientId = request.PatientId;
        ApplyCommonPlanFields(plan, request.Title, request.Description, request.Objective, request.StartDate, request.EndDate, request.DailyCalories, request.ProteinGrams, request.CarbsGrams, request.FatsGrams, request.FiberGrams, request.WaterIntakeLiters, request.Supplements, request.Instructions, request.Notes, request.Meals);
        plan.Version += 1;
    }

    internal static async Task<IReadOnlyCollection<DietPlanItem>> GetPlanItemsAsync(IRepository<DietPlanItem> itemRepository, long dietPlanId)
    {
        var items = await itemRepository.GetAsync(query => query
            .Where(i => i.DietPlanId == dietPlanId && i.DeletedAt == null)
            .OrderBy(i => i.DayNumber)
            .ThenBy(i => i.MealTime)
            .ThenBy(i => i.MealType));
        return items.ToArray();
    }

    internal static async Task<IEnumerable<DietPlanDto>> ToDtosAsync(IRepository<DietPlanItem> itemRepository, IEnumerable<DietPlan> plans)
    {
        var result = new List<DietPlanDto>();
        foreach (var plan in plans)
        {
            var items = await GetPlanItemsAsync(itemRepository, plan.Id);
            result.Add(ToDto(plan, items));
        }

        return result;
    }

    internal static DietPlanDto ToDto(DietPlan plan, IEnumerable<DietPlanItem> items)
    {
        return new DietPlanDto
        {
            Id = plan.Id,
            PatientId = plan.PatientId,
            NutritionistId = plan.NutritionistId,
            Title = plan.Title,
            Description = plan.Description,
            Objective = plan.Objective,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            Duration = plan.Duration,
            Version = plan.Version,
            ReviewDate = plan.ReviewDate,
            PublishedAt = plan.PublishedAt,
            RowVersion = plan.RowVersion,
            Status = plan.Status,
            DailyCalories = plan.DailyCalories,
            ProteinGrams = plan.ProteinGrams,
            CarbsGrams = plan.CarbsGrams,
            FatsGrams = plan.FatsGrams,
            FiberGrams = plan.FiberGrams,
            WaterIntakeLiters = plan.WaterIntakeLiters,
            Supplements = plan.Supplements,
            Instructions = plan.Instructions,
            Notes = plan.Notes,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
            Meals = items.Select(ToItemDto).ToArray()
        };
    }

    internal static DietPlanComplianceDto ToComplianceDto(DietPlanCompliance compliance)
    {
        return new DietPlanComplianceDto
        {
            Id = compliance.Id,
            DietPlanId = compliance.DietPlanId,
            PatientId = compliance.PatientId,
            RecordDate = compliance.RecordDate,
            MealsCompleted = compliance.MealsCompleted,
            TotalMeals = compliance.TotalMeals,
            WaterIntakeLiters = compliance.WaterIntakeLiters,
            TookSupplements = compliance.TookSupplements,
            Notes = compliance.Notes,
            CompliancePercentage = compliance.CompliancePercentage,
            CreatedAt = compliance.CreatedAt,
            UpdatedAt = compliance.UpdatedAt
        };
    }

    private static void ApplyCommonPlanFields(
        DietPlan plan,
        string title,
        string? description,
        string? objective,
        DateTime startDate,
        DateTime endDate,
        int dailyCalories,
        decimal proteinGrams,
        decimal carbsGrams,
        decimal fatsGrams,
        decimal? fiberGrams,
        decimal? waterIntakeLiters,
        string? supplements,
        string? instructions,
        string? notes,
        IEnumerable<UpsertDietPlanItemRequest> meals)
    {
        plan.Title = title;
        plan.Description = description;
        plan.Objective = objective;
        plan.StartDate = startDate.Date;
        plan.EndDate = endDate.Date;
        plan.Duration = Math.Max(1, (endDate.Date - startDate.Date).Days + 1);
        plan.DailyCalories = dailyCalories;
        plan.ProteinGrams = proteinGrams;
        plan.CarbsGrams = carbsGrams;
        plan.FatsGrams = fatsGrams;
        plan.FiberGrams = fiberGrams;
        plan.WaterIntakeLiters = waterIntakeLiters;
        plan.Supplements = supplements;
        plan.Instructions = instructions;
        plan.Notes = notes;
        plan.MealFrequency = meals.Count();
        plan.Items = plan.Items.Where(i => i.DeletedAt != null).Concat(meals.Select(ToItem)).ToList();
    }

    private static DietPlanItem ToItem(UpsertDietPlanItemRequest request)
    {
        return new DietPlanItem
        {
            DayNumber = request.DayNumber,
            MealType = request.MealType,
            MealTime = request.MealTime,
            ItemName = request.ItemName,
            Quantity = request.Quantity,
            Unit = request.Unit,
            Calories = request.Calories,
            Protein = request.Protein,
            Carbs = request.Carbs,
            Fats = request.Fats,
            Fiber = request.Fiber,
            Notes = request.Notes
        };
    }

    private static DietPlanItemDto ToItemDto(DietPlanItem item)
    {
        return new DietPlanItemDto
        {
            Id = item.Id,
            DietPlanId = item.DietPlanId,
            DayNumber = item.DayNumber,
            MealType = item.MealType,
            MealTime = item.MealTime,
            ItemName = item.ItemName,
            Quantity = item.Quantity,
            Unit = item.Unit,
            Calories = item.Calories,
            Protein = item.Protein,
            Carbs = item.Carbs,
            Fats = item.Fats,
            Fiber = item.Fiber,
            Notes = item.Notes,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}

