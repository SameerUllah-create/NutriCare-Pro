namespace NutriCare.API.Controllers;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;
using NutriCare.API.Privacy;

[ApiController, Route("api/privacy"), Authorize]
public class PrivacyController(NutriCareDbContext db, ICurrentUserService current, PatientDataExportService exports) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResponse<object>.SuccessResponse(await exports.ReadRecordsAsync(ct)));
    }

    [HttpGet("export/bundle")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> ExportBundle(CancellationToken ct)
    {
        var download = await exports.CreateBundleAsync(ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(download.Content, "application/zip", download.FileName);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponse<object>.SuccessResponse(
        await db.DataRightsRequests.AsNoTracking().Where(r => r.UserId == current.UserId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct)));

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest(RightsRequest input, CancellationToken ct)
    {
        if (input.RequestType is not ("Deletion" or "Export")) throw new ArgumentException("Choose Deletion or Export");
        if (await db.DataRightsRequests.AnyAsync(r => r.UserId == current.UserId && r.RequestType == input.RequestType && r.Status == "Pending", ct))
            throw new ConflictException("A request of this type is already pending");
        var row = new DataRightsRequest { UserId = current.UserId, RequestType = input.RequestType, Reason = input.Reason };
        db.Add(row); await db.SaveChangesAsync(ct);
        return StatusCode(201, ApiResponse<object>.SuccessResponse(row, "Request recorded for review; retention decisions require an approved policy",201));
    }
}

public class RightsRequest
{
    [Required] public string RequestType { get; set; } = "Deletion";
    [StringLength(2000)] public string? Reason { get; set; }
}
