namespace NutriCare.API.Middleware;

using System.Text.Json;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Global exception handling middleware
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    public static int StatusCodeFor(Exception exception) => exception switch
    {
        EntityNotFoundException => 404,
        ValidationException or FluentValidation.ValidationException or ArgumentException or JsonException => 400,
        UnauthorizedException or InvalidCredentialsException or InvalidTokenException or UnauthorizedAccessException => 401,
        ForbiddenAccessException => 403,
        ConflictException or UserAlreadyExistsException or DbUpdateConcurrencyException => 409,
        _ => 500
    };
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
            await HandleExceptionAsync(context, ex, _logger);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, ILogger logger)
    {
        context.Response.ContentType = "application/json";

        var traceId = context.TraceIdentifier;
        var statusCode = StatusCodes.Status500InternalServerError;
        var message = "An unexpected error occurred.";

        switch (exception)
        {
            case EntityNotFoundException ex:
                statusCode = StatusCodes.Status404NotFound;
                message = ex.Message;
                break;

            case ValidationException ex:
                statusCode = StatusCodes.Status400BadRequest;
                message = ex.Message;
                break;

            case FluentValidation.ValidationException:
                statusCode = StatusCodes.Status400BadRequest;
                message = "Validation failure";
                break;

            case InvalidCredentialsException:
            case InvalidTokenException:
            case UnauthorizedAccessException:
                statusCode = StatusCodes.Status401Unauthorized;
                message = "Authentication or signature validation failed";
                break;

            case UserAlreadyExistsException:
                statusCode = StatusCodes.Status409Conflict;
                message = "An account could not be created with these details";
                break;

            case DbUpdateConcurrencyException:
                statusCode = StatusCodes.Status409Conflict;
                message = "This record changed. Reload it before saving again.";
                break;

            case UnauthorizedException ex:
                statusCode = StatusCodes.Status401Unauthorized;
                message = ex.Message;
                break;

            case ForbiddenAccessException ex:
                statusCode = StatusCodes.Status403Forbidden;
                message = ex.Message;
                break;

            case ConflictException ex:
                statusCode = StatusCodes.Status409Conflict;
                message = ex.Message;
                break;

            case ArgumentException ex:
                statusCode = StatusCodes.Status400BadRequest;
                message = ex.Message;
                break;

            case JsonException:
                statusCode = StatusCodes.Status400BadRequest;
                message = "The request body is not valid JSON";
                break;

            default:
                logger.LogWarning("Returning generic error response for trace {TraceId}", traceId);
                break;
        }

        context.Response.StatusCode = statusCode;

        var response = ApiResponse<object>.FailureResponse(message, statusCode);
        var errors = exception is ValidationException validation ? validation.Errors
            : exception is FluentValidation.ValidationException fluent ? fluent.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            : null;
        response.Data = new { traceId, errors };

        return context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// Extension method for adding exception handling middleware
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}
