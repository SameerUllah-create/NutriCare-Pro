namespace NutriCare.Application.Commands.DietPlans;

using MediatR;
using NutriCare.Application.DTOs;

public class CreateDietPlanCommand : IRequest<DietPlanDto>
{
    public long NutritionistId { get; set; }
    public long PatientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Objective { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DailyCalories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatsGrams { get; set; }
    public decimal? FiberGrams { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public string? Supplements { get; set; }
    public string? Instructions { get; set; }
    public string? Notes { get; set; }
    public DateTime? ReviewDate { get; set; }
    public string? ChangeReason { get; set; }
    public IEnumerable<UpsertDietPlanItemRequest> Meals { get; set; } = new List<UpsertDietPlanItemRequest>();
}

public class UpdateDietPlanCommand : IRequest<DietPlanDto>
{
    public long DietPlanId { get; set; }
    public long NutritionistId { get; set; }
    public long PatientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Objective { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DailyCalories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatsGrams { get; set; }
    public decimal? FiberGrams { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public string? Supplements { get; set; }
    public string? Instructions { get; set; }
    public string? Notes { get; set; }
    public DateTime? ReviewDate { get; set; }
    public int? ExpectedVersion { get; set; }
    public string? ChangeReason { get; set; }
    public IEnumerable<UpsertDietPlanItemRequest> Meals { get; set; } = new List<UpsertDietPlanItemRequest>();
}

public class TrackDietPlanComplianceCommand : IRequest<DietPlanComplianceDto>
{
    public long DietPlanId { get; set; }
    public long PatientId { get; set; }
    public DateTime RecordDate { get; set; }
    public int MealsCompleted { get; set; }
    public int TotalMeals { get; set; }
    public decimal WaterIntakeLiters { get; set; }
    public bool TookSupplements { get; set; }
    public string? Notes { get; set; }
}
