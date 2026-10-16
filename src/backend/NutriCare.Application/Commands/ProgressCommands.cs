namespace NutriCare.Application.Commands.Progress;

using MediatR;
using NutriCare.Application.DTOs;

public class TrackProgressCommand : IRequest<ProgressRecordDto>
{
    public long PatientId { get; set; }
    public DateTime RecordDate { get; set; }
    public decimal Weight { get; set; }
    public decimal? BMI { get; set; }
    public decimal? BodyFatPercentage { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public int? DailyCalories { get; set; }
    public int? ExerciseMinutes { get; set; }
    public string? ExerciseType { get; set; }
    public string? Notes { get; set; }
    public decimal? WaistCircumference { get; set; }
    public decimal? ChestCircumference { get; set; }
    public decimal? HipCircumference { get; set; }
    public decimal? ArmCircumference { get; set; }
    public decimal? ThighCircumference { get; set; }
    public byte[]? ExpectedVersion { get; set; }
    public string? ChangeReason { get; set; }
}
