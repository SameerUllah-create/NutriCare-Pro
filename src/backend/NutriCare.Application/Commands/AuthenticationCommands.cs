namespace NutriCare.Application.Commands.Authentication;

using MediatR;
using NutriCare.Application.DTOs;

/// <summary>
/// Login command for user authentication
/// </summary>
public class LoginCommand : IRequest<LoginResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Register command for new user registration
/// </summary>
public class RegisterCommand : IRequest<UserDto>
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Role { get; set; } = "Patient";
    public string VerificationUrlBase { get; set; } = string.Empty;
}

/// <summary>
/// Refresh token command
/// </summary>
public class RefreshTokenCommand : IRequest<RefreshTokenResponse>
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Logout command
/// </summary>
public class LogoutCommand : IRequest<bool>
{
    public long UserId { get; set; }
    public string? RefreshToken { get; set; }
}

public class ForgotPasswordCommand : IRequest<bool>
{
    public string Email { get; set; } = string.Empty;
    public string ResetUrlBase { get; set; } = string.Empty;
}

public class ResetPasswordCommand : IRequest<bool>
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class VerifyEmailCommand : IRequest<bool>
{
    public long UserId { get; set; }
    public string Token { get; set; } = string.Empty;
}

public class ResendEmailVerificationCommand : IRequest<bool>
{
    public string Email { get; set; } = string.Empty;
    public string VerificationUrlBase { get; set; } = string.Empty;
}

public class AssignRoleCommand : IRequest<UserDto>
{
    public long UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class RemoveRoleCommand : IRequest<UserDto>
{
    public long UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}
