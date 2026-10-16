namespace NutriCare.Application.Queries.Progress;

using MediatR;
using NutriCare.Application.DTOs;

public class GetProgressHistoryQuery : IRequest<IEnumerable<ProgressRecordDto>>
{
    public long PatientId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class GetProgressChartDataQuery : IRequest<IEnumerable<ProgressChartPointDto>>
{
    public long PatientId { get; set; }
    public string Metric { get; set; } = "weight";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class GetProgressAnalyticsQuery : IRequest<ProgressAnalyticsDto>
{
    public long PatientId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
