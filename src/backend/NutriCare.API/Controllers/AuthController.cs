namespace NutriCare.API.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NutriCare.Application.Commands.Authentication;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Authentication;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("Authentication")]
public class AuthController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    private string Frontend(string path) => $"{(configuration["FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/')}/{path}";

    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request) => Ok(ApiResponse<LoginResponse>.SuccessResponse(
        await mediator.Send(new LoginCommand { Email = request.Email, Password = request.Password }), "Login successful"));

    [HttpPost("register"), AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = await mediator.Send(new RegisterCommand { Email = request.Email, FirstName = request.FirstName,
            LastName = request.LastName, Password = request.Password, ConfirmPassword = request.ConfirmPassword,
            Role = request.Role, VerificationUrlBase = Frontend("verify-email") });
        return StatusCode(201, ApiResponse<UserDto>.SuccessResponse(user, "Check your email to verify your account", 201));
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(RefreshTokenRequest? request)
    {
        await mediator.Send(new LogoutCommand { UserId = long.Parse(User.FindFirst("uid")!.Value), RefreshToken = request?.RefreshToken });
        return Ok(ApiResponse<object>.SuccessResponse(new { loggedOut = true }));
    }

    [HttpPost("forgot-password"), AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await mediator.Send(new ForgotPasswordCommand { Email = request.Email, ResetUrlBase = Frontend("reset-password") });
        return Ok(ApiResponse<object>.SuccessResponse(new { sent = true }, "If the account exists, reset instructions will be sent"));
    }

    [HttpPost("reset-password"), AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        await mediator.Send(new ResetPasswordCommand { Email = request.Email, Token = request.Token, NewPassword = request.NewPassword, ConfirmPassword = request.ConfirmPassword });
        return Ok(ApiResponse<object>.SuccessResponse(new { reset = true }));
    }

    [HttpGet("verify-email"), AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromQuery] long userId, [FromQuery] string token)
    {
        await mediator.Send(new VerifyEmailCommand { UserId = userId, Token = token });
        return Ok(ApiResponse<object>.SuccessResponse(new { verified = true }));
    }

    [HttpPost("resend-verification"), AllowAnonymous]
    public async Task<IActionResult> ResendVerification(ResendEmailVerificationRequest request)
    {
        await mediator.Send(new ResendEmailVerificationCommand { Email = request.Email, VerificationUrlBase = Frontend("verify-email") });
        return Ok(ApiResponse<object>.SuccessResponse(new { sent = true }, "If verification is needed, instructions will be sent"));
    }

    [HttpPost("refresh-token"), AllowAnonymous]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequest request) => Ok(ApiResponse<RefreshTokenResponse>.SuccessResponse(
        await mediator.Send(new RefreshTokenCommand { RefreshToken = request.RefreshToken })));

    [HttpPut("users/{userId:long}/role"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignRole(long userId, RoleRequest request) => Ok(ApiResponse<UserDto>.SuccessResponse(
        await mediator.Send(new AssignRoleCommand { UserId = userId, Role = request.Role })));

    [HttpDelete("users/{userId:long}/role"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveRole(long userId, RoleRequest request) => Ok(ApiResponse<UserDto>.SuccessResponse(
        await mediator.Send(new RemoveRoleCommand { UserId = userId, Role = request.Role })));

    [HttpGet("{id:long}"), Authorize]
    public async Task<IActionResult> GetUser(long id) => Ok(ApiResponse<UserDto>.SuccessResponse(await mediator.Send(new GetUserByIdQuery { UserId = id })));

    [HttpPost("verify"), Authorize]
    public IActionResult VerifyToken() => Ok(ApiResponse<object>.SuccessResponse(new { valid = true }));
}
