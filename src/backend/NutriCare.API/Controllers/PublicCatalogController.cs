namespace NutriCare.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Persistence.Contexts;

[ApiController, Route("api"), AllowAnonymous]
public sealed class PublicCatalogController(NutriCareDbContext db) : ControllerBase
{
    [HttpGet("services")]
    public async Task<IActionResult> Services(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return Ok(ApiResponse<object>.SuccessResponse(await db.Services.AsNoTracking().Where(s => s.DeletedAt == null && s.IsPublished && s.IsActive && (s.EffectiveFrom == null || s.EffectiveFrom <= now) && (s.EffectiveTo == null || s.EffectiveTo > now) && db.Nutritionists.Any(n=>n.Id==s.NutritionistId && n.DeletedAt==null && n.IsVerified && n.User!=null && n.User.IsActive && n.User.EmailConfirmed && n.User.DeletedAt==null)).OrderBy(s => s.Name).ToListAsync(ct)));
    }

    [HttpGet("packages")]
    public async Task<IActionResult> Packages(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return Ok(ApiResponse<object>.SuccessResponse(await db.ServicePackages.AsNoTracking().Where(s => s.DeletedAt == null && s.IsPublished && s.IsActive &&
            s.NutritionistApprovedById != null && s.NutritionistApprovedAt != null && s.NutritionistApprovedVersion == s.Version &&
            (s.EffectiveFrom == null || s.EffectiveFrom <= now) && (s.EffectiveTo == null || s.EffectiveTo > now) &&
            db.Nutritionists.Any(n => n.UserId == s.NutritionistApprovedById && n.DeletedAt == null && n.IsVerified && n.User != null && n.User.IsActive && n.User.EmailConfirmed && n.User.DeletedAt == null))
            .OrderBy(s => s.DurationDays).ToListAsync(ct)));
    }

    [HttpGet("policies")]
    public async Task<IActionResult> Policies(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return Ok(ApiResponse<object>.SuccessResponse(await db.PolicyDocuments.AsNoTracking().Where(s => s.DeletedAt == null && s.IsPublished && (s.EffectiveFrom == null || s.EffectiveFrom <= now) && (s.EffectiveTo == null || s.EffectiveTo > now)).ToListAsync(ct)));
    }

    [HttpGet("cancellation-policy")]
    public async Task<IActionResult> CancellationPolicy(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var policy = await db.CancellationPolicies.AsNoTracking().Where(p => p.DeletedAt == null && p.IsPublished && (p.EffectiveFrom == null || p.EffectiveFrom <= now) && (p.EffectiveTo == null || p.EffectiveTo > now)).OrderByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(ct);
        return Ok(ApiResponse<object?>.SuccessResponse(policy));
    }

    [HttpGet("content/{kind}")]
    public async Task<IActionResult> Content(string kind, CancellationToken ct) =>
        Ok(ApiResponse<object>.SuccessResponse(await db.PublicContents.AsNoTracking().Where(c => c.DeletedAt == null && c.IsPublished && c.Kind == kind).OrderBy(c => c.Id).Select(c => new {c.Id,c.Kind,c.Title,c.TitleUrdu,c.Content,c.ContentUrdu}).ToListAsync(ct)));

    [HttpGet("blogs")]
    public async Task<IActionResult> Blogs(CancellationToken ct) =>
        Ok(ApiResponse<object>.SuccessResponse(await db.Blogs.AsNoTracking().Where(b => b.DeletedAt == null && b.Status == "Published").OrderByDescending(b => b.PublishedAt).Select(b => new { b.Id, b.Title, b.Slug, b.Summary, b.Category, b.Tags, b.CoverImageUrl, b.PublishedAt }).Take(100).ToListAsync(ct)));

    [HttpGet("blogs/{slug}")]
    public async Task<IActionResult> Blog(string slug, CancellationToken ct)
    {
        var blog = await db.Blogs.AsNoTracking().Where(b => b.DeletedAt == null && b.Status == "Published" && b.Slug == slug).Select(b => new { b.Id, b.Title, b.Slug, b.Summary, b.Content, b.Category, b.Tags, b.CoverImageUrl, b.PublishedAt }).SingleOrDefaultAsync(ct);
        return blog == null ? NotFound(ApiResponse<object>.FailureResponse("Article not found",404)) : Ok(ApiResponse<object>.SuccessResponse(blog));
    }
}
