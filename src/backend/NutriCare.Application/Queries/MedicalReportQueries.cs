namespace NutriCare.Application.Queries.MedicalReports;

using MediatR;
using NutriCare.Application.DTOs;

public class GetMedicalReportsQuery : IRequest<IEnumerable<MedicalReportDto>>
{
    public long PatientId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
    public string? ReportType { get; set; }
}

public class GetMedicalReportByIdQuery : IRequest<MedicalReportDto>
{
    public long ReportId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
}

public class DownloadMedicalReportQuery : IRequest<MedicalReportFileDto>
{
    public long ReportId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
}
