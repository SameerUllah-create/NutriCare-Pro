namespace NutriCare.Infrastructure.Authentication;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public sealed class AuthSessionService(IRepository<AuthSession> sessions, IUnitOfWork unitOfWork,
    UserManager<User> users, IJwtTokenService tokens, IConfiguration configuration) : IAuthSessionService
{
    public async Task<AuthTokens> IssueAsync(User user, string role)
    {
        var session = new AuthSession
        {
            UserId = user.Id,
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddDays(configuration.GetValue("Jwt:RefreshTokenExpirationDays", 7))
        };
        var pair = await tokens.GenerateTokenPairAsync(user.Id, user.Email!, role, session.SecurityStamp, session.SessionId);
        session.RefreshTokenHash = Hash(pair.RefreshToken);
        await sessions.AddAsync(session);
        await unitOfWork.SaveChangesAsync();
        return new(pair.AccessToken, pair.RefreshToken);
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) throw new InvalidTokenException("Invalid refresh token");
        var hash = Hash(refreshToken);
        await unitOfWork.BeginTransactionAsync($"auth:refresh:{hash}");
        try
        {
            var candidates = await sessions.GetAsync(q => q.Where(s => s.RefreshTokenHash == hash || s.PreviousRefreshTokenHash == hash));
            var session = candidates.FirstOrDefault();
            if (session == null || session.RevokedAt != null || session.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidTokenException("Invalid or expired refresh token");
            if (session.PreviousRefreshTokenHash == hash)
            {
                session.RevokedAt = DateTime.UtcNow;
                await sessions.UpdateAsync(session);
                await unitOfWork.CommitAsync();
                throw new InvalidTokenException("Refresh token has already been used");
            }
            var user = await users.FindByIdAsync(session.UserId.ToString());
            if (user == null || !user.IsActive || user.DeletedAt != null || !user.EmailConfirmed || user.SecurityStamp != session.SecurityStamp)
                throw new InvalidTokenException("Session is no longer valid");
            var role = (await users.GetRolesAsync(user)).FirstOrDefault() ?? "Patient";
            var pair = await tokens.GenerateTokenPairAsync(user.Id, user.Email!, role, user.SecurityStamp, session.SessionId);
            session.PreviousRefreshTokenHash = session.RefreshTokenHash;
            session.RefreshTokenHash = Hash(pair.RefreshToken);
            session.LastUsedAt = DateTime.UtcNow;
            await sessions.UpdateAsync(session);
            await unitOfWork.CommitAsync();
            return new(pair.AccessToken, pair.RefreshToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task RevokeAsync(long userId, string? refreshToken)
    {
        var hash = string.IsNullOrWhiteSpace(refreshToken) ? null : Hash(refreshToken);
        var active = await sessions.GetAsync(q => q.Where(s => s.UserId == userId && s.RevokedAt == null && (hash == null || s.RefreshTokenHash == hash || s.PreviousRefreshTokenHash == hash)));
        foreach (var session in active)
        {
            session.RevokedAt = DateTime.UtcNow;
            await sessions.UpdateAsync(session);
        }
        await unitOfWork.SaveChangesAsync();
    }

    private static string Hash(string value) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
