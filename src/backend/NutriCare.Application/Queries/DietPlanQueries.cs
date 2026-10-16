namespace NutriCare.Application.Queries.DietPlans;

using MediatR;
using NutriCare.Application.DTOs;

public class GetDietPlanByIdQuery : IRequest<DietPlanDto>
{
    public long DietPlanId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
}

public class GetPatientDietPlansQuery : IRequest<IEnumerable<DietPlanDto>>
{
    public long PatientId { get; set; }
    public string? Status { get; set; }
}

public class GetNutritionistDietPlansQuery : IRequest<IEnumerable<DietPlanDto>>
{
    public long NutritionistId { get; set; }
    public long? PatientId { get; set; }
    public string? Status { get; set; }
}

public class DownloadDietPlanPdfQuery : IRequest<DietPlanPdfDto>
{
    public long DietPlanId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
}

public class GetDietPlanComplianceQuery : IRequest<IEnumerable<DietPlanComplianceDto>>
{
    public long DietPlanId { get; set; }
    public long PatientId { get; set; }
}

public class DietPlanPdfDto
{
    public string FileName { get; set; } = "diet-plan.pdf";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
