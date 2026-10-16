namespace NutriCare.API.Middleware;

using System.Security.Claims;
using NutriCare.Domain.Entities;
using NutriCare.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

public class AuditLoggingMiddleware
{
    private static readonly HashSet<string> AuditedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDbContextFactory<NutriCareDbContext> factory)
    {
        if (!ShouldAudit(context))
        {
            await _next(context);
            return;
        }

        Exception? capturedException = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            try
            {
                // Never save a failed request's tracked clinical or financial changes as part of auditing.
                await using var dbContext = await factory.CreateDbContextAsync();
                await dbContext.AuditLogs.AddAsync(new AuditLog
                {
                    UserId = GetUserId(context.User),
                    Action = context.Request.Method,
                    EntityType = NormalizeEntityType(context.Request.Path),
                    EntityId = context.Request.Path.Value?.Split('/').Select(segment => long.TryParse(segment,out var id) ? id : 0).FirstOrDefault(id => id > 0) ?? 0,
                    Description = $"{context.Request.Method} {context.Request.Path}",
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers.UserAgent.FirstOrDefault() is string agent ? agent[..Math.Min(agent.Length,512)] : null,
                    StatusCode = capturedException == null ? context.Response.StatusCode : GlobalExceptionHandlingMiddleware.StatusCodeFor(capturedException),
                    IsSuccess = capturedException == null && context.Response.StatusCode < 400,
                    ErrorMessage = capturedException?.GetType().Name
                });

                await dbContext.SaveChangesAsync();
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(auditEx, "Failed to write audit log for {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);
            }
        }
    }

    private static bool ShouldAudit(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/api")
            && !context.Request.Path.StartsWithSegments("/api/health")
            && (AuditedMethods.Contains(context.Request.Method) ||
                context.User.Identity?.IsAuthenticated == true &&
                new[] { "/api/medicalreports", "/api/dietplans", "/api/clinical", "/api/progress", "/api/patients", "/api/privacy", "/api/patientdashboard", "/api/nutritionistdashboard" }
                    .Any(path => context.Request.Path.StartsWithSegments(path)));
    }

    private static long? GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : null;
    }

    private static string NormalizeEntityType(PathString path)
    {
        var segments = path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return segments is { Length: > 1 } ? segments[1] : "Unknown";
    }
}

public static class AuditLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditLoggingMiddleware>();
    }
}
