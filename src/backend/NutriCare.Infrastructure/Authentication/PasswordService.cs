namespace NutriCare.Infrastructure.Authentication;

using System.Security.Cryptography;
using System.Text;
using NutriCare.Domain.Interfaces;

/// <summary>
/// Password service implementation using bcrypt
/// </summary>
public class PasswordService : IPasswordService
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}
