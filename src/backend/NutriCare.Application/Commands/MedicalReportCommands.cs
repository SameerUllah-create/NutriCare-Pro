namespace NutriCare.Application.Commands.MedicalReports;

using MediatR;
using NutriCare.Application.DTOs;

public class UploadMedicalReportCommand : IRequest<MedicalReportDto>
{
    public long PatientId { get; set; }
    public long UploadedByUserId { get; set; }
    public string ReportType { get; set; } = "LabReport";
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public Stream Content { get; set; } = Stream.Null;
    public string? PatientNotes { get; set; }
}

public class AddMedicalReportNotesCommand : IRequest<MedicalReportDto>
{
    public long ReportId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
