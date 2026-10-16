namespace NutriCare.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Contexts;

/// <summary>Existing paid entitlements only. Package checkout is deliberately not exposed.</summary>
[ApiController, Route("api/package-enrollments"), Authorize(Roles = "Admin,Patient")]
public sealed class PackageEnrollmentsController(NutriCareDbContext db, PackageCreditStore credits, ICurrentUserService current) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (page is < 1 or > 10000) return BadRequest(ApiResponse<object>.FailureResponse("Choose a valid page", 400));
        var query = Scope();
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(e => e.PurchasedAtUtc).Skip((page - 1) * 50).Take(50)
            .Select(e => new { e.Id, e.ServicePackageId, e.PatientId, e.PackageVersion, e.PurchasedAtUtc, e.FirstConsultationDueAtUtc, e.ActivatedAtUtc, e.ExpiresAtUtc, e.Status, e.RowVersion }).ToListAsync(ct);
        return Ok(ApiResponse<object>.SuccessResponse(new { items, total, page, pageSize = 50, checkoutEnabled = false }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        var enrollment = await Scope().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (enrollment == null) return NotFound(ApiResponse<object>.FailureResponse("Package enrollment not found", 404));
        var usage = await db.PackageCreditUsages.AsNoTracking().Where(u => u.PackageEnrollmentId == id)
            .OrderBy(u => u.Id).Select(u => new { u.Id, u.CreditKind, u.ResourceType, u.ResourceId, u.Status, u.ServiceCode, u.CreatedAt }).Take(1000).ToListAsync(ct);
        var history = await db.PackageCreditHistory.AsNoTracking().Where(h => h.PackageEnrollmentId == id)
            .OrderByDescending(h => h.CreatedAt).Select(h => new { h.Id, h.Action, h.Reason, h.ActorUserId, h.PreviousExpiryUtc, h.NewExpiryUtc, h.CreatedAt }).Take(200).ToListAsync(ct);
        return Ok(ApiResponse<object>.SuccessResponse(new { enrollment, usage, history, checkoutEnabled = false }));
    }

    [HttpPost("{id:long}/extend"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Extend(long id, ExtendPackageRequest input, CancellationToken ct)
    {
        byte[] version;
        try { version = Convert.FromBase64String(input.RowVersion ?? ""); }
        catch (FormatException) { return BadRequest(ApiResponse<object>.FailureResponse("Supply a valid row version", 400)); }
        var enrollment = await credits.ExtendAsync(id, input.Days, input.Reason ?? "", version, current.UserId, ct);
        return Ok(ApiResponse<object>.SuccessResponse(enrollment, "Validity extended with an audit record"));
    }

    private IQueryable<PackageEnrollment> Scope() => db.PackageEnrollments.AsNoTracking().Where(e => e.DeletedAt == null &&
        (current.Role == "Admin" || db.Patients.Any(p => p.Id == e.PatientId && p.UserId == current.UserId && p.DeletedAt == null)));
    public sealed record ExtendPackageRequest(int Days, string? Reason, string? RowVersion);
}
