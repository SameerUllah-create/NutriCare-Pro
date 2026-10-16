namespace NutriCare.Application.Handlers.Authentication;

using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Identity;
using NutriCare.Application.Commands.Authentication;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Authentication;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly IAuthSessionService _sessions;

    public LoginCommandHandler(UserManager<User> userManager, IAuthSessionService sessions)
    {
        _userManager = userManager;
        _sessions = sessions;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || await _userManager.IsLockedOutAsync(user))
        {
            throw new InvalidCredentialsException();
        }
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            throw new InvalidCredentialsException();
        }
        if (user.TwoFactorEnabled) throw new UnauthorizedException("This account requires multi-factor authentication");

        if (!user.IsActive || user.IsDeleted)
        {
            throw new UnauthorizedException("User account is inactive");
        }

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedException("Please verify your email before logging in");
        }

        var role = await AuthHandlerHelpers.GetPrimaryRoleAsync(_userManager, user);
        await _userManager.ResetAccessFailedCountAsync(user);
        var (accessToken, refreshToken) = await _sessions.IssueAsync(user, role);

        user.LastLoginAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        AuthHandlerHelpers.EnsureIdentitySucceeded(updateResult);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = await AuthHandlerHelpers.ToUserDtoAsync(_userManager, user)
        };
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, UserDto>
{
    private readonly IRepository<Patient> _patients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IEmailService _emailService;

    public RegisterCommandHandler(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IEmailService emailService, IRepository<Patient> patients, IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _patients = patients;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (request.Role != "Patient") throw new ForbiddenAccessException("Public registration creates patient accounts only");
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new UserAlreadyExistsException(request.Email);
        }

        await AuthHandlerHelpers.EnsureRoleExistsAsync(_roleManager, request.Role);

        var user = new User
        {
            UserName = request.Email,
            NormalizedUserName = request.Email.ToUpperInvariant(),
            Email = request.Email,
            NormalizedEmail = request.Email.ToUpperInvariant(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = false,
            IsActive = true
        };

        await _unitOfWork.BeginTransactionAsync($"registration:{request.Email.Trim().ToUpperInvariant()}");
        try
        {
        var createResult = await _userManager.CreateAsync(user, request.Password);
        AuthHandlerHelpers.EnsureIdentitySucceeded(createResult);

        var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        AuthHandlerHelpers.EnsureIdentitySucceeded(roleResult);
        await _patients.AddAsync(new Patient { UserId = user.Id });
        await _unitOfWork.CommitAsync();
        }
        catch { await _unitOfWork.RollbackAsync(); throw; }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var verificationUrlBase = string.IsNullOrWhiteSpace(request.VerificationUrlBase)
            ? "/api/auth/verify-email"
            : request.VerificationUrlBase;
        await _emailService.SendVerificationEmailAsync(
            user.Email!,
            $"{verificationUrlBase}?userId={user.Id}&token={Uri.EscapeDataString(token)}");

        return await AuthHandlerHelpers.ToUserDtoAsync(_userManager, user);
    }
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    private readonly IAuthSessionService _sessions;
    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _tokenService;

    public RefreshTokenCommandHandler(UserManager<User> userManager, IJwtTokenService tokenService, IAuthSessionService sessions)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _sessions = sessions;
    }

    public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var pair = await _sessions.RefreshAsync(request.RefreshToken);
        return new RefreshTokenResponse { AccessToken = pair.AccessToken, RefreshToken = pair.RefreshToken };
    }
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly IAuthSessionService _sessions;
    private readonly UserManager<User> _userManager;

    public LogoutCommandHandler(UserManager<User> userManager, IAuthSessionService sessions)
    {
        _userManager = userManager;
        _sessions = sessions;
    }

    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await _sessions.RevokeAsync(request.UserId, request.RefreshToken);
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken) ||
            user.RefreshTokenHash == AuthHandlerHelpers.HashToken(request.RefreshToken))
        {
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiryDate = null;
            var result = await _userManager.UpdateAsync(user);
            AuthHandlerHelpers.EnsureIdentitySucceeded(result);
        }

        return true;
    }
}

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, bool>
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(UserManager<User> userManager, IEmailService emailService)
    {
        _userManager = userManager;
        _emailService = emailService;
    }

    public async Task<bool> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive || user.IsDeleted)
        {
            return true;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = $"{request.ResetUrlBase}?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
        await _emailService.SendPasswordResetEmailAsync(user.Email!, resetLink);
        return true;
    }
}

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly UserManager<User> _userManager;

    public ResetPasswordCommandHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive || user.IsDeleted)
        {
            throw new InvalidTokenException("Invalid password reset token");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        AuthHandlerHelpers.EnsureIdentitySucceeded(result);

        user.RefreshTokenHash = null;
        user.RefreshTokenExpiryDate = null;
        var updateResult = await _userManager.UpdateAsync(user);
        AuthHandlerHelpers.EnsureIdentitySucceeded(updateResult);

        return true;
    }
}

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, bool>
{
    private readonly UserManager<User> _userManager;

    public VerifyEmailCommandHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<bool> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null || user.IsDeleted)
        {
            throw new InvalidTokenException("Invalid email verification token");
        }

        var result = await _userManager.ConfirmEmailAsync(user, request.Token);
        AuthHandlerHelpers.EnsureIdentitySucceeded(result);
        return true;
    }
}

public class ResendEmailVerificationCommandHandler : IRequestHandler<ResendEmailVerificationCommand, bool>
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailService _emailService;

    public ResendEmailVerificationCommandHandler(UserManager<User> userManager, IEmailService emailService)
    {
        _userManager = userManager;
        _emailService = emailService;
    }

    public async Task<bool> Handle(ResendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || user.EmailConfirmed || !user.IsActive || user.IsDeleted)
        {
            return true;
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = $"{request.VerificationUrlBase}?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await _emailService.SendVerificationEmailAsync(user.Email!, link);
        return true;
    }
}

public class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, UserDto>
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public AssignRoleCommandHandler(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<UserDto> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new EntityNotFoundException(nameof(User), request.UserId);

        await AuthHandlerHelpers.EnsureRoleExistsAsync(_roleManager, request.Role);
        var currentRoles = await _userManager.GetRolesAsync(user);
        var managedRoles = currentRoles.Where(r => r is "Admin" or "Nutritionist" or "Patient").ToArray();

        if (managedRoles.Length > 0)
        {
            AuthHandlerHelpers.EnsureIdentitySucceeded(await _userManager.RemoveFromRolesAsync(user, managedRoles));
        }

        AuthHandlerHelpers.EnsureIdentitySucceeded(await _userManager.AddToRoleAsync(user, request.Role));
        AuthHandlerHelpers.EnsureIdentitySucceeded(await _userManager.UpdateSecurityStampAsync(user));
        return await AuthHandlerHelpers.ToUserDtoAsync(_userManager, user);
    }
}

public class RemoveRoleCommandHandler : IRequestHandler<RemoveRoleCommand, UserDto>
{
    private readonly UserManager<User> _userManager;

    public RemoveRoleCommandHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserDto> Handle(RemoveRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new EntityNotFoundException(nameof(User), request.UserId);

        if (await _userManager.IsInRoleAsync(user, request.Role))
        {
            AuthHandlerHelpers.EnsureIdentitySucceeded(await _userManager.RemoveFromRoleAsync(user, request.Role));
            AuthHandlerHelpers.EnsureIdentitySucceeded(await _userManager.UpdateSecurityStampAsync(user));
        }

        return await AuthHandlerHelpers.ToUserDtoAsync(_userManager, user);
    }
}

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<User> _userManager;

    public GetUserByIdQueryHandler(UserManager<User> userManager, ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId != request.UserId && _currentUser.Role != "Admin") throw new ForbiddenAccessException("You may only view your own account");
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());

        if (user == null || user.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(User), request.UserId);
        }

        return await AuthHandlerHelpers.ToUserDtoAsync(_userManager, user);
    }
}

internal static class AuthHandlerHelpers
{
    internal static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    internal static async Task<string> GetPrimaryRoleAsync(UserManager<User> userManager, User user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles.FirstOrDefault() ?? "Patient";
    }

    internal static async Task EnsureRoleExistsAsync(RoleManager<Role> roleManager, string role)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(new Role(role)
            {
                Description = $"{role} role"
            });
            EnsureIdentitySucceeded(result);
        }
    }

    internal static async Task<UserDto> ToUserDtoAsync(UserManager<User> userManager, User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            ProfileImageUrl = user.ProfileImageUrl,
            Role = await GetPrimaryRoleAsync(userManager, user),
            IsEmailVerified = user.EmailConfirmed,
            IsPhoneVerified = user.PhoneNumberConfirmed,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    internal static void EnsureIdentitySucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var message = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new ValidationException(message);
    }
}
