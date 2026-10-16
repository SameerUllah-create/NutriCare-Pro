namespace NutriCare.API.Controllers;

using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Persistence.Contexts;
using NutriCare.Application.Services;
using NutriCare.Domain.Interfaces;
using NutriCare.Domain.Exceptions;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly NutriCareDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AdminDashboardController(NutriCareDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<AdminDashboardSummaryDto>>> GetSummary(CancellationToken cancellationToken)
    {
        var completedPayments = _context.Payments.Where(p => p.DeletedAt == null && p.PaidAt != null && p.Currency == "PKR");
        var grossRevenue = await completedPayments.SumAsync(p => p.Amount, cancellationToken);
        var refundedRevenue = await _context.PaymentRefunds.Where(r => r.DeletedAt == null && r.Status == "Completed" && r.Currency == "PKR")
            .SumAsync(r => r.Amount, cancellationToken);

        var summary = new AdminDashboardSummaryDto
        {
            Currency = "PKR",
            ExcludedCurrencyCount = await _context.Payments.CountAsync(p => p.DeletedAt == null && p.Currency != "PKR", cancellationToken),
            TotalUsers = await _context.Users.CountAsync(u => u.DeletedAt == null, cancellationToken),
            ActiveUsers = await _context.Users.CountAsync(u => u.DeletedAt == null && u.IsActive, cancellationToken),
            TotalPatients = await _context.Patients.CountAsync(p => p.DeletedAt == null, cancellationToken),
            TotalNutritionists = await _context.Nutritionists.CountAsync(n => n.DeletedAt == null, cancellationToken),
            VerifiedNutritionists = await _context.Nutritionists.CountAsync(n => n.DeletedAt == null && n.IsVerified, cancellationToken),
            TotalAppointments = await _context.Appointments.CountAsync(a => a.DeletedAt == null, cancellationToken),
            UpcomingAppointments = await _context.Appointments.CountAsync(a =>
                a.DeletedAt == null && a.AppointmentDateTime >= DateTime.UtcNow && a.Status != "Cancelled" && a.Status != "Rejected",
                cancellationToken),
            CompletedAppointments = await _context.Appointments.CountAsync(a => a.DeletedAt == null && a.Status == "Completed", cancellationToken),
            PublishedBlogs = await _context.Blogs.CountAsync(b => b.DeletedAt == null && b.Status == "Published", cancellationToken),
            TotalRevenue = grossRevenue,
            RefundedRevenue = refundedRevenue,
            NetRevenue = grossRevenue - refundedRevenue
        };

        return Ok(ApiResponse<AdminDashboardSummaryDto>.SuccessResponse(summary));
    }

    [HttpGet("analytics/revenue")]
    public async Task<ActionResult<ApiResponse<AdminRevenueAnalyticsDto>>> GetRevenueAnalytics(
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken cancellationToken)
    {
        var range = NormalizeRange(fromDate, toDate);
        var paid = await _context.Payments.AsNoTracking().Where(p => p.DeletedAt == null && p.Currency == "PKR" &&
            p.PaidAt >= range.From && p.PaidAt <= range.To)
            .Select(p => new { p.Amount, p.PaidAt, p.PaymentMethod }).ToListAsync(cancellationToken);
        var refunds = await _context.PaymentRefunds.AsNoTracking().Where(r => r.DeletedAt == null && r.Status == "Completed" &&
            r.Currency == "PKR" && r.RefundedAt >= range.From && r.RefundedAt <= range.To)
            .Select(r => new { r.Amount, r.RefundedAt, Provider = r.Payment!.PaymentMethod }).ToListAsync(cancellationToken);
        var events = paid.Select(p => new RevenueEvent(p.PaidAt!.Value, p.PaymentMethod, p.Amount))
            .Concat(refunds.Select(r => new RevenueEvent(r.RefundedAt!.Value, r.Provider, -r.Amount))).ToArray();
        var zone = ClinicalTime.Zone(ClinicalTime.DefaultZone);
        var gross = paid.Sum(p => p.Amount);
        var refunded = refunds.Sum(r => r.Amount);
        var result = new AdminRevenueAnalyticsDto
        {
            Currency = "PKR",
            ExcludedCurrencyCount = await _context.Payments.CountAsync(p => p.DeletedAt == null && p.Currency != "PKR" &&
                p.PaidAt >= range.From && p.PaidAt <= range.To, cancellationToken),
            FromDate = range.From, ToDate = range.To, GrossRevenue = gross, RefundedRevenue = refunded, NetRevenue = gross - refunded,
            RevenueByDay = events.GroupBy(e => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(e.At, DateTimeKind.Utc), zone).Date)
                .OrderBy(g => g.Key).Select(g => new AdminChartPointDto { Label = g.Key.ToString("yyyy-MM-dd"), Date = g.Key, Value = g.Sum(e => e.Amount) }).ToArray(),
            RevenueByProvider = events.GroupBy(e => e.Provider).Select(g => new AdminChartPointDto { Label = g.Key, Value = g.Sum(e => e.Amount) })
                .OrderByDescending(g => g.Value).ToArray()
        };
        return Ok(ApiResponse<AdminRevenueAnalyticsDto>.SuccessResponse(result));
    }
    [HttpGet("analytics/appointments")]
    public async Task<ActionResult<ApiResponse<AdminAppointmentAnalyticsDto>>> GetAppointmentAnalytics(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var range = NormalizeRange(fromDate, toDate);
        var appointments = await _context.Appointments
            .Where(a => a.DeletedAt == null && a.AppointmentDateTime >= range.From && a.AppointmentDateTime <= range.To)
            .ToListAsync(cancellationToken);

        var result = new AdminAppointmentAnalyticsDto
        {
            FromDate = range.From,
            ToDate = range.To,
            TotalAppointments = appointments.Count,
            AppointmentsByDay = appointments
                .GroupBy(a => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(a.AppointmentDateTime, DateTimeKind.Utc), ClinicalTime.Zone(ClinicalTime.DefaultZone)).Date)
                .OrderBy(g => g.Key)
                .Select(g => new AdminChartPointDto { Label = g.Key.ToString("yyyy-MM-dd"), Date = g.Key, Value = g.Count() })
                .ToArray(),
            AppointmentsByStatus = appointments
                .GroupBy(a => a.Status)
                .OrderByDescending(g => g.Count())
                .Select(g => new AdminChartPointDto { Label = g.Key, Value = g.Count() })
                .ToArray(),
            AppointmentsByType = appointments
                .GroupBy(a => a.AppointmentType)
                .OrderByDescending(g => g.Count())
                .Select(g => new AdminChartPointDto { Label = g.Key, Value = g.Count() })
                .ToArray()
        };

        return Ok(ApiResponse<AdminAppointmentAnalyticsDto>.SuccessResponse(result));
    }

    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdminUserListItemDto>>>> GetUsers(
        [FromQuery] string? role = null,
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var roleMap = await GetUserRoleMapAsync(cancellationToken);
        var users = await _context.Users
            .Where(u => u.DeletedAt == null)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        var filtered = users
            .Select(u => ToAdminUserDto(u, roleMap.GetValueOrDefault(u.Id) ?? "Unassigned"))
            .Where(u => string.IsNullOrWhiteSpace(role) || u.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
            .Where(u => string.IsNullOrWhiteSpace(search)
                || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                || u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 200))
            .ToArray();

        return Ok(ApiResponse<IEnumerable<AdminUserListItemDto>>.SuccessResponse(filtered));
    }

    [HttpPut("users/{id:long}/status")]
    public async Task<ActionResult<ApiResponse<AdminUserListItemDto>>> UpdateUserStatus(
        long id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null, cancellationToken);
        if (user == null)
        {
            return NotFound(ApiResponse<AdminUserListItemDto>.FailureResponse("User was not found", 404));
        }

        if (id == _currentUser.UserId && !request.IsActive) throw new ConflictException("Use another administrator account to deactivate your own account");
        user.IsActive = request.IsActive;
        if (!request.IsActive)
        {
            var sessions = await _context.AuthSessions.Where(s => s.UserId == id && s.RevokedAt == null).ToListAsync(cancellationToken);
            foreach (var session in sessions) session.RevokedAt = DateTime.UtcNow;
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiryDate = null;
        }
        await _context.SaveChangesAsync(cancellationToken);

        var roleMap = await GetUserRoleMapAsync(cancellationToken);
        return Ok(ApiResponse<AdminUserListItemDto>.SuccessResponse(
            ToAdminUserDto(user, roleMap.GetValueOrDefault(user.Id) ?? "Unassigned"),
            "User status updated"));
    }

    [HttpGet("patients")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdminPatientListItemDto>>>> GetPatients(
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.CanAccessClinicalData) throw new ForbiddenAccessException("Clinical-access permission is required. Manage nonclinical account details through the users list.");
        var patients = await _context.Patients
            .Include(p => p.User)
            .Include(p => p.Appointments)
            .Where(p => p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = patients
            .Select(p => new AdminPatientListItemDto
            {
                Id = p.Id,
                UserId = p.UserId,
                FullName = FullName(p.User),
                Email = p.User?.Email ?? string.Empty,
                CurrentWeight = p.CurrentWeight,
                TargetWeight = p.TargetWeight,
                City = p.City,
                AppointmentCount = p.Appointments.Count(a => a.DeletedAt == null),
                CreatedAt = p.CreatedAt
            })
            .Where(p => string.IsNullOrWhiteSpace(search)
                || p.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || p.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 200))
            .ToArray();

        return Ok(ApiResponse<IEnumerable<AdminPatientListItemDto>>.SuccessResponse(result));
    }

    [HttpGet("nutritionists")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdminNutritionistListItemDto>>>> GetNutritionists(
        [FromQuery] string? search = null,
        [FromQuery] bool? verified = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var nutritionists = await _context.Nutritionists
            .Include(n => n.User)
            .Include(n => n.Appointments)
            .Where(n => n.DeletedAt == null)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = nutritionists
            .Where(n => !verified.HasValue || n.IsVerified == verified.Value)
            .Select(n => new AdminNutritionistListItemDto
            {
                Id = n.Id,
                UserId = n.UserId,
                FullName = FullName(n.User),
                Email = n.User?.Email ?? string.Empty,
                LicenseNumber = n.LicenseNumber,
                Qualification = n.Qualification,
                ConsultationFee = n.ConsultationFee,
                IsVerified = n.IsVerified,
                RatingAverage = n.RatingAverage,
                AppointmentCount = n.Appointments.Count(a => a.DeletedAt == null),
                CreatedAt = n.CreatedAt
            })
            .Where(n => string.IsNullOrWhiteSpace(search)
                || n.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || n.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                || n.LicenseNumber.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 200))
            .ToArray();

        return Ok(ApiResponse<IEnumerable<AdminNutritionistListItemDto>>.SuccessResponse(result));
    }

    [HttpPut("nutritionists/{id:long}/verification")]
    public async Task<ActionResult<ApiResponse<AdminNutritionistListItemDto>>> UpdateNutritionistVerification(
        long id,
        [FromBody] UpdateNutritionistVerificationRequest request,
        CancellationToken cancellationToken)
    {
        return Conflict(ApiResponse<AdminNutritionistListItemDto>.FailureResponse("Use POST /api/admin/nutritionist/{id}/verify with credential evidence, explicit approval and the current concurrency token", 409));
    }

    [HttpGet("blogs")]
    public async Task<ActionResult<ApiResponse<IEnumerable<BlogDto>>>> GetBlogs(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var blogs = await _context.Blogs
            .Include(b => b.Author)
            .Where(b => b.DeletedAt == null)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = blogs
            .Where(b => string.IsNullOrWhiteSpace(status) || b.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .Where(b => string.IsNullOrWhiteSpace(search)
                || b.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || b.Summary.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(ToBlogDto)
            .ToArray();

        return Ok(ApiResponse<IEnumerable<BlogDto>>.SuccessResponse(result));
    }

    [HttpPost("blogs")]
    public async Task<ActionResult<ApiResponse<BlogDto>>> CreateBlog([FromBody] UpsertBlogRequest request, CancellationToken cancellationToken)
    {
        ValidateBlog(request);
        var blog = new Blog
        {
            AuthorUserId = GetCurrentUserId(),
            Title = request.Title.Trim(),
            Slug = await CreateUniqueSlugAsync(request.Slug, request.Title, null, cancellationToken),
            Summary = request.Summary.Trim(),
            Content = request.Content,
            CoverImageUrl = request.CoverImageUrl,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            Tags = request.Tags.Trim(),
            Status = "Draft",
            PublishedAt = null
        };

        await _context.Blogs.AddAsync(blog, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(blog).Reference(b => b.Author).LoadAsync(cancellationToken);
        await RecordBlogRevisionAsync(blog, "Created draft", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetBlogs), new { id = blog.Id },
            ApiResponse<BlogDto>.SuccessResponse(ToBlogDto(blog), "Blog created successfully", 201));
    }

    [HttpPut("blogs/{id:long}")]
    public async Task<ActionResult<ApiResponse<BlogDto>>> UpdateBlog(long id, [FromBody] UpsertBlogRequest request, CancellationToken cancellationToken)
    {
        ValidateBlog(request);
        var blog = await _context.Blogs
            .Include(b => b.Author)
            .FirstOrDefaultAsync(b => b.Id == id && b.DeletedAt == null, cancellationToken);

        if (blog == null)
        {
            return NotFound(ApiResponse<BlogDto>.FailureResponse("Blog was not found", 404));
        }

        RequireBlogVersion(blog, request.ExpectedVersion);
        await RecordBlogRevisionAsync(blog, "Before edit", cancellationToken);
        blog.Title = request.Title.Trim();
        blog.Slug = await CreateUniqueSlugAsync(request.Slug, request.Title, blog.Id, cancellationToken);
        blog.Summary = request.Summary.Trim();
        blog.Content = request.Content;
        blog.CoverImageUrl = request.CoverImageUrl;
        blog.Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        blog.Tags = request.Tags.Trim();
        blog.PublishedAt = null;
        blog.Status = "Draft";

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<BlogDto>.SuccessResponse(ToBlogDto(blog), "Blog updated successfully"));
    }

    [HttpPost("blogs/{id:long}/publish")]
    public async Task<ActionResult<ApiResponse<BlogDto>>> PublishBlog(long id, [FromBody] PublishClinicalBlogRequest request, CancellationToken cancellationToken)
    {
        if (!request.Approved) throw new ArgumentException("Explicit approval of the reviewed professional content is required");
        var blog = await _context.Blogs.Include(b => b.Author).SingleOrDefaultAsync(b => b.Id == id && b.DeletedAt == null, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Blog), id);
        RequireBlogVersion(blog, request.ExpectedVersion);
        ValidateBlog(new UpsertBlogRequest { Title = blog.Title, Summary = blog.Summary, Content = blog.Content, Category = blog.Category, Tags = blog.Tags, CoverImageUrl = blog.CoverImageUrl });
        blog.Status = "Published";
        blog.PublishedAt ??= DateTime.UtcNow;
        await RecordBlogRevisionAsync(blog, "Explicit professional-content approval and publication", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<BlogDto>.SuccessResponse(ToBlogDto(blog)));
    }

    [HttpDelete("blogs/{id:long}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteBlog(long id, CancellationToken cancellationToken)
    {
        var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.Id == id && b.DeletedAt == null, cancellationToken);
        if (blog == null)
        {
            return NotFound(ApiResponse<bool>.FailureResponse("Blog was not found", 404));
        }

        blog.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Blog deleted successfully"));
    }

    [HttpGet("audit-logs")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdminAuditLogDto>>>> GetAuditLogs(
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var logs = await _context.AuditLogs
            .Where(a => a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = logs
            .Where(a => string.IsNullOrWhiteSpace(entityType) || a.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase))
            .Where(a => string.IsNullOrWhiteSpace(action) || a.Action.Equals(action, StringComparison.OrdinalIgnoreCase))
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 500))
            .Select(a => new AdminAuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Description = a.Description,
                IpAddress = a.IpAddress,
                StatusCode = a.StatusCode,
                IsSuccess = a.IsSuccess,
                ErrorMessage = a.ErrorMessage,
                CreatedAt = a.CreatedAt
            })
            .ToArray();

        return Ok(ApiResponse<IEnumerable<AdminAuditLogDto>>.SuccessResponse(result));
    }

    private async Task<Dictionary<long, string>> GetUserRoleMapAsync(CancellationToken cancellationToken)
    {
        return await (
            from userRole in _context.Set<IdentityUserRole<long>>()
            join role in _context.Roles on userRole.RoleId equals role.Id
            select new { userRole.UserId, RoleName = role.Name ?? string.Empty })
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(g => g.Key, g => string.Join(",", g.Select(x => x.RoleName)), cancellationToken);
    }

    private async Task<string> CreateUniqueSlugAsync(string? requestedSlug, string title, long? existingBlogId, CancellationToken cancellationToken)
    {
        var baseSlug = Slugify(string.IsNullOrWhiteSpace(requestedSlug) ? title : requestedSlug);
        var slug = baseSlug;
        var suffix = 2;

        while (await _context.Blogs.AnyAsync(b => b.Slug == slug && b.Id != existingBlogId && b.DeletedAt == null, cancellationToken))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }

    private static AdminUserListItemDto ToAdminUserDto(User user, string role)
    {
        return new AdminUserListItemDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = FullName(user),
            Role = role,
            IsActive = user.IsActive,
            IsEmailVerified = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    private static BlogDto ToBlogDto(Blog blog)
    {
        return new BlogDto
        {
            Id = blog.Id,
            RowVersion = blog.RowVersion,
            CreatedAt = blog.CreatedAt,
            UpdatedAt = blog.UpdatedAt,
            AuthorUserId = blog.AuthorUserId,
            AuthorName = FullName(blog.Author),
            Title = blog.Title,
            Slug = blog.Slug,
            Summary = blog.Summary,
            Content = blog.Content,
            CoverImageUrl = blog.CoverImageUrl,
            Category = blog.Category,
            Tags = blog.Tags,
            Status = blog.Status,
            PublishedAt = blog.PublishedAt
        };
    }

    private static void ValidateBlog(UpsertBlogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 || string.IsNullOrWhiteSpace(request.Summary) || request.Summary.Length > 2000 ||
            string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 100000 || request.Tags?.Length > 1000 || request.Category?.Length > 100 || request.Slug?.Length > 200)
            throw new ArgumentException("Supply a title, summary and article within the supported size limits");
        if (!string.IsNullOrWhiteSpace(request.CoverImageUrl) && (!Uri.TryCreate(request.CoverImageUrl, UriKind.Absolute, out var image) || image.Scheme != "https"))
            throw new ArgumentException("Cover images must use an absolute HTTPS URL");
    }

    private static void RequireBlogVersion(Blog blog, byte[]? expected)
    {
        if (expected == null || !expected.SequenceEqual(blog.RowVersion)) throw new ConflictException("The article changed; reload the saved draft before editing or approving it");
    }

    private async Task RecordBlogRevisionAsync(Blog blog, string action, CancellationToken cancellationToken)
    {
        var previous = await _context.ConfigurationRevisions.Where(r => r.EntityType == "Blog" && r.EntityId == blog.Id)
            .Select(r => (int?)r.Version).MaxAsync(cancellationToken) ?? 0;
        _context.ConfigurationRevisions.Add(new ConfigurationRevision { EntityType = "Blog", EntityId = blog.Id, Version = previous + 1,
            Action = action, SnapshotJson = JsonSerializer.Serialize(ToBlogDto(blog)), ChangedByUserId = _currentUser.UserId });
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? $"post-{DateTime.UtcNow:yyyyMMddHHmmss}" : slug;
    }

    private static string FullName(User? user)
    {
        if (user == null)
        {
            return string.Empty;
        }

        return $"{user.FirstName} {user.LastName}".Trim();
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : 0;
    }

    private static (DateTime From, DateTime To) NormalizeRange(DateTime? fromDate, DateTime? toDate)
    {
        var today = ClinicalTime.LocalToday();
        var from = (fromDate ?? today.AddDays(-30)).Date;
        var to = (toDate ?? today).Date.AddDays(1);
        if (from >= to || (to - from).TotalDays > 366) throw new ArgumentException("Choose a valid reporting period of at most 366 days");
        return (ClinicalTime.ToUtc(DateTime.SpecifyKind(from, DateTimeKind.Unspecified)), ClinicalTime.ToUtc(DateTime.SpecifyKind(to, DateTimeKind.Unspecified)).AddTicks(-1));
    }

    private sealed record RevenueEvent(DateTime At, string Provider, decimal Amount);
}

public sealed class PublishClinicalBlogRequest
{
    public bool Approved { get; set; }
    public byte[]? ExpectedVersion { get; set; }
}
