namespace NutriCare.Domain.Interfaces;

using NutriCare.Domain.Entities;

public record AuthTokens(string AccessToken, string RefreshToken);

public interface IAuthSessionService
{
    Task<AuthTokens> IssueAsync(User user, string role);
    Task<AuthTokens> RefreshAsync(string refreshToken);
    Task RevokeAsync(long userId, string? refreshToken);
}
