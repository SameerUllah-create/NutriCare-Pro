namespace NutriCare.API.Setup;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Entities;
using NutriCare.Persistence.Contexts;

public static class InitialAdministrator
{
    // Opt-in, one-time provisioning; no default password or automatic email confirmation.
    public static async Task ProvisionAsync(IServiceProvider services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Bootstrap:Enabled")) return;
        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Bootstrap requires an administrator email and a password from secure configuration");
        var db = services.GetRequiredService<NutriCareDbContext>();
        if (await db.UserRoles.AnyAsync(r => r.RoleId == 1)) return;
        var users = services.GetRequiredService<UserManager<User>>();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (await db.UserRoles.AnyAsync(r => r.RoleId == 1)) return;
        if (await users.FindByEmailAsync(email) != null) throw new InvalidOperationException("Bootstrap does not promote existing accounts");
        var user = new User { Email = email, UserName = email, FirstName = "Practice", LastName = "Administrator", EmailConfirmed = false };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) throw new InvalidOperationException("Administrator provisioning failed password or account validation");
        var assigned = await users.AddToRoleAsync(user, "Admin");
        if (!assigned.Succeeded) throw new InvalidOperationException("Administrator role assignment failed");
        await tx.CommitAsync();
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var origin = (configuration["FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
        await services.GetRequiredService<NutriCare.Domain.Interfaces.IEmailService>().SendVerificationEmailAsync(email,
            $"{origin}/verify-email?userId={user.Id}&token={Uri.EscapeDataString(token)}");
    }
}
