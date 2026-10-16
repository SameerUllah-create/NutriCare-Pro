namespace NutriCare.API.Controllers;

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Contexts;

[ApiController, Route("api/configuration"), Authorize(Roles = "Admin,Nutritionist")]
public sealed class ConfigurationController(NutriCareDbContext db, ConfigurationStore store, ICurrentUserService current) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        long? clinicianId = current.Role == "Admin" ? null : await db.Nutritionists.Where(n => n.UserId == current.UserId && n.DeletedAt == null).Select(n => (long?)n.Id).SingleOrDefaultAsync(ct);
        if (current.Role == "Nutritionist" && clinicianId == null) return Forbid();
        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            services = await db.Services.AsNoTracking().Where(s => s.DeletedAt == null && (clinicianId == null || s.NutritionistId == null || s.NutritionistId == clinicianId)).ToListAsync(ct),
            packages = await db.ServicePackages.AsNoTracking().Where(s => s.DeletedAt == null).ToListAsync(ct),
            availabilityRules = await db.AvailabilityRules.AsNoTracking().Where(s => s.DeletedAt == null && (clinicianId == null || s.NutritionistId == 0 || s.NutritionistId == clinicianId)).ToListAsync(ct),
            exceptions = await db.AvailabilityExceptions.AsNoTracking().Where(s => s.DeletedAt == null && (clinicianId == null || s.NutritionistId == clinicianId)).ToListAsync(ct),
            cancellationPolicies = await db.CancellationPolicies.AsNoTracking().Where(s => s.DeletedAt == null).ToListAsync(ct),
            policyDocuments = await db.PolicyDocuments.AsNoTracking().Where(s => s.DeletedAt == null).ToListAsync(ct),
            history = await HistoryQuery().OrderByDescending(h => h.CreatedAt).Take(100).ToListAsync(ct),
            clinicians = await db.Nutritionists.AsNoTracking().Where(n => n.DeletedAt == null && (clinicianId == null || n.Id == clinicianId)).Select(n => new { n.Id, n.UserId, n.IsVerified, IsActive = n.User != null && n.User.IsActive && n.User.DeletedAt == null, IsEmailVerified = n.User != null && n.User.EmailConfirmed, n.RowVersion, n.LicenseNumber, n.Qualification, Name = n.User == null ? "" : n.User.FirstName + " " + n.User.LastName }).ToListAsync(ct)
        }));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] string? kind, [FromQuery] long? id, CancellationToken ct)
    {
        var query = HistoryQuery();
        if (kind != null) query = query.Where(h => h.EntityType == kind);
        if (id != null) query = query.Where(h => h.EntityId == id);
        return Ok(ApiResponse<object>.SuccessResponse(await query.OrderByDescending(h => h.CreatedAt).Take(200).ToListAsync(ct)));
    }

    [HttpPost("{kind}")]
    public async Task<IActionResult> Create(string kind, JsonElement input, CancellationToken ct) =>
        StatusCode(201, ApiResponse<object>.SuccessResponse(await store.SaveAsync(kind, null, input, ct), "Draft saved; approval is required before publication", 201));

    [HttpPut("{kind}/{id:long}")]
    public async Task<IActionResult> Update(string kind, long id, JsonElement input, CancellationToken ct) =>
        Ok(ApiResponse<object>.SuccessResponse(await store.SaveAsync(kind, id, input, ct), "Draft updated"));

    [HttpPost("{kind}/{id:long}/publish")]
    public async Task<IActionResult> Publish(string kind, long id, JsonElement input, CancellationToken ct) =>
        Ok(ApiResponse<object>.SuccessResponse(await store.PublishAsync(kind, id, input, true, ct), "Published after explicit approval"));

    [HttpPost("{kind}/{id:long}/unpublish")]
    public async Task<IActionResult> Unpublish(string kind, long id, JsonElement input, CancellationToken ct) =>
        Ok(ApiResponse<object>.SuccessResponse(await store.PublishAsync(kind, id, input, false, ct), "Unpublished"));

    [HttpPost("{kind}/{id:long}/revision")]
    public async Task<IActionResult> Revision(string kind, long id, CancellationToken ct) =>
        StatusCode(201, ApiResponse<object>.SuccessResponse(await store.CreateRevisionAsync(kind, id, ct), "New draft created; the published version is unchanged", 201));

    [HttpPost("packages/{id:long}/nutritionist-approval")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<IActionResult> ApprovePackage(long id, JsonElement input, CancellationToken ct) =>
        Ok(ApiResponse<object>.SuccessResponse(await store.ApprovePackageAsync(id, input, ct), "Package content reviewed; separate publication is still required"));

    private IQueryable<NutriCare.Domain.Entities.ConfigurationRevision> HistoryQuery() => db.ConfigurationRevisions.AsNoTracking().Where(h =>
        current.Role == "Admin" || h.ChangedByUserId == current.UserId || h.Action == "Seeded proposed draft" ||
        h.EntityType == "seed" || h.EntityType == "packages" || h.EntityType == "policies" || h.EntityType == "cancellation-policies");
}
