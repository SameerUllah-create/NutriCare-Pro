namespace NutriCare.API.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriCare.Application.Commands.MedicalReports;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.MedicalReports;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MedicalReportsController : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IMediator _mediator;
    private readonly IRepository<Patient> _patientRepository;
    private readonly long _maxFileSizeBytes;

    public MedicalReportsController(
        IMediator mediator,
        IRepository<Patient> patientRepository,
        IConfiguration configuration)
    {
        _mediator = mediator;
        _patientRepository = patientRepository;
        _maxFileSizeBytes = configuration.GetValue<long?>("AzureBlobStorage:MaxMedicalReportSizeBytes") ?? 10 * 1024 * 1024;
    }

    [HttpPost]
    [Authorize(Roles = "Patient")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<MedicalReportDto>>> UploadReport(
        IFormFile file,
        [FromForm] string reportType = "LabReport",
        [FromForm] string? patientNotes = null)
    {
        ValidateFile(file);
        var patient = await GetCurrentPatientAsync();

        await using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new UploadMedicalReportCommand
        {
            PatientId = patient.Id,
            UploadedByUserId = GetCurrentUserId(),
            ReportType = reportType,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            Content = stream,
            PatientNotes = patientNotes
        });

        return CreatedAtAction(nameof(GetReport), new { id = result.Id },
            ApiResponse<MedicalReportDto>.SuccessResponse(result, "Medical report uploaded successfully", 201));
    }

    [HttpGet("my-reports")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MedicalReportDto>>>> GetMyReports([FromQuery] string? reportType = null)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new GetMedicalReportsQuery
        {
            PatientId = patient.Id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole(),
            ReportType = reportType
        });

        return Ok(ApiResponse<IEnumerable<MedicalReportDto>>.SuccessResponse(result));
    }

    [HttpGet("patient/{patientId:long}")]
    [Authorize(Roles = "Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MedicalReportDto>>>> GetPatientReports(
        long patientId,
        [FromQuery] string? reportType = null)
    {
        var result = await _mediator.Send(new GetMedicalReportsQuery
        {
            PatientId = patientId,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole(),
            ReportType = reportType
        });

        return Ok(ApiResponse<IEnumerable<MedicalReportDto>>.SuccessResponse(result));
    }

    [HttpGet("{id:long}")]
    [Authorize(Roles = "Patient,Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<MedicalReportDto>>> GetReport(long id)
    {
        var result = await _mediator.Send(new GetMedicalReportByIdQuery
        {
            ReportId = id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole()
        });

        return Ok(ApiResponse<MedicalReportDto>.SuccessResponse(result));
    }

    [HttpGet("{id:long}/download")]
    [Authorize(Roles = "Patient,Nutritionist,Admin")]
    public async Task<IActionResult> DownloadReport(long id)
    {
        var result = await _mediator.Send(new DownloadMedicalReportQuery
        {
            ReportId = id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole()
        });

        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPut("{id:long}/notes")]
    [Authorize(Roles = "Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<MedicalReportDto>>> AddNotes(long id, [FromBody] AddMedicalReportNotesRequest request)
    {
        var result = await _mediator.Send(new AddMedicalReportNotesCommand
        {
            ReportId = id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole(),
            Notes = request.Notes
        });

        return Ok(ApiResponse<MedicalReportDto>.SuccessResponse(result, "Report notes saved successfully"));
    }

    private void ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("A report file is required");
        }

        if (file.Length > _maxFileSizeBytes)
        {
            throw new ArgumentException($"File size must not exceed {_maxFileSizeBytes / 1024 / 1024} MB");
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new ArgumentException("Only PDF, JPEG, PNG, and WEBP files are allowed");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!MedicalReportFileSignature.IsAllowedExtension(extension))
        {
            throw new ArgumentException("Only .pdf, .jpg, .jpeg, .png, and .webp files are allowed");
        }

        var expectedContentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => string.Empty
        };
        if (!string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The declared file type must match the file extension");
        }

        using var stream = file.OpenReadStream();
        if (!MedicalReportFileSignature.IsValid(stream, extension))
        {
            throw new ArgumentException("The uploaded file content does not match its extension");
        }
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

    private string GetCurrentRole()
    {
        return User.FindFirst("role")?.Value ?? string.Empty;
    }
}

internal static class MedicalReportFileSignature
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    internal static bool IsAllowedExtension(string extension) => Extensions.Contains(extension);

    internal static bool IsValid(Stream stream, string extension)
    {
        Span<byte> header = stackalloc byte[12];
        var bytesRead = stream.Read(header);
        var bytes = header[..bytesRead];

        return extension switch
        {
            ".pdf" => bytesRead >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46,
            ".jpg" or ".jpeg" => bytesRead >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytesRead >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                      bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            ".webp" => bytesRead >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                       bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
            _ => false
        };
    }
}
