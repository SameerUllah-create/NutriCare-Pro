namespace NutriCare.Infrastructure.Authentication;

using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using NutriCare.Domain.Interfaces;

/// <summary>
/// Current user service implementation
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    public bool CanAccessClinicalData => _httpContextAccessor.HttpContext?.User.HasClaim("clinical_access", "true") == true;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("uid");
            return long.TryParse(claim?.Value, out var userId) ? userId : 0;
        }
    }

    public string Email
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value
                ?? _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value
                ?? string.Empty;
        }
    }

    public string Role
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.FindFirst("role")?.Value
                ?? _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value
                ?? string.Empty;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
        }
    }
}
