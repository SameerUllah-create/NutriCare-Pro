namespace NutriCare.Tests.Integrations;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;
using NutriCare.Tests.Configuration;

[Trait("Category", "SqlIntegration")]
public class SqlAuthenticationHttpTests(IsolatedSqlDatabase database) : IClassFixture<IsolatedSqlDatabase>
{
    private const string Password = "TestPatient49Password";

    [Fact]
    public async Task PublicSignupCreatesPatientAndRejectsPrivilegedRoles()
    {
        using var host = new SqlAuthHost(database.ConnectionString); using var client = Client(host);
        foreach (var role in new[] { "Admin", "Nutritionist" })
        {
            var response = await client.PostAsJsonAsync("/api/auth/register", Signup(role));
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Forbidden });
        }
        var email = UniqueEmail();
        var created = await client.PostAsJsonAsync("/api/auth/register", Signup("Patient", email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await Data(created)).GetProperty("id").GetInt64();
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
        Assert.True(await db.Patients.AnyAsync(p => p.UserId == id));
        Assert.False((await db.Users.SingleAsync(u => u.Id == id)).EmailConfirmed);
        Assert.True(host.Mail.VerificationLinks.ContainsKey(email));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode);
    }

    [Fact]
    public async Task PatientCanReadOwnAccountButCannotReadAnotherAccount()
    {
        using var host = new SqlAuthHost(database.ConnectionString); using var client = Client(host);
        var first = await RegisterVerified(host, client); var second = await RegisterVerified(host, client);
        var login = await Login(client, first.Email); Authorize(client, login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/auth/{first.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/auth/{second.Id}")).StatusCode);
    }

    [Fact]
    public async Task LogoutImmediatelyRejectsAccessAndRefreshTokens()
    {
        using var host = new SqlAuthHost(database.ConnectionString); using var client = Client(host);
        var user = await RegisterVerified(host, client); var login = await Login(client, user.Email); Authorize(client, login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/verify", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = login.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/verify", new { })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = login.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task RefreshReplayRevokesTheRotatedSession()
    {
        using var host = new SqlAuthHost(database.ConnectionString); using var client = Client(host);
        var user = await RegisterVerified(host, client); var login = await Login(client, user.Email);
        var response = await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var rotated = await Data(response);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = login.RefreshToken })).StatusCode);
        Authorize(client, rotated.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/verify", new { })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = rotated.GetProperty("refreshToken").GetString() })).StatusCode);
    }

    [Fact]
    public async Task PasswordResetInvalidatesOldSessionAndUsesFrontendFormLink()
    {
        using var host = new SqlAuthHost(database.ConnectionString); using var client = Client(host);
        var user = await RegisterVerified(host, client); var login = await Login(client, user.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email })).StatusCode);
        var resetLink = host.Mail.ResetLinks[user.Email]; Assert.StartsWith("https://portal.example.test/reset-password?", resetLink, StringComparison.Ordinal);
        var token = QueryHelpers.ParseQuery(new Uri(resetLink).Query)["token"].ToString();
        const string newPassword = "ChangedTestPatient81Password";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/reset-password", new { email = user.Email, token, newPassword, confirmPassword = newPassword })).StatusCode);
        Authorize(client, login.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/verify", new { })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = login.RefreshToken })).StatusCode);
        await Login(client, user.Email, newPassword);
    }

    [Fact]
    public async Task AdministratorDeactivationImmediatelyRevokesPatientAccess()
    {
        using var host = new SqlAuthHost(database.ConnectionString); using var patientClient = Client(host); using var adminClient = Client(host);
        var patient = await RegisterVerified(host, patientClient); var admin = await CreateAdmin(host);
        var patientLogin = await Login(patientClient, patient.Email); Authorize(patientClient, patientLogin.AccessToken);
        var adminLogin = await Login(adminClient, admin); Authorize(adminClient, adminLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync($"/api/admin/users/{patient.Id}/status", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await patientClient.PostAsJsonAsync("/api/auth/verify", new { })).StatusCode);
        patientClient.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await patientClient.PostAsJsonAsync("/api/auth/login", new { email = patient.Email, password = Password })).StatusCode);
    }

    [Fact]
    public async Task RoleChangeInvalidatesThePreviousRoleToken()
    {
        using var host = new SqlAuthHost(database.ConnectionString); using var patientClient = Client(host); using var adminClient = Client(host);
        var patient = await RegisterVerified(host, patientClient); var admin = await CreateAdmin(host);
        var patientLogin = await Login(patientClient, patient.Email); Authorize(patientClient, patientLogin.AccessToken);
        var adminLogin = await Login(adminClient, admin); Authorize(adminClient, adminLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync($"/api/auth/users/{patient.Id}/role", new { role = "Nutritionist" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await patientClient.PostAsJsonAsync("/api/auth/verify", new { })).StatusCode);
        patientClient.DefaultRequestHeaders.Authorization = null;
        Assert.Equal("Nutritionist", (await Login(patientClient, patient.Email)).Role);
    }

    private static object Signup(string role, string? email = null) => new { email = email ?? UniqueEmail(), firstName = "Test", lastName = "Patient", password = Password, confirmPassword = Password, role };
    private static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.test";
    private static HttpClient Client(SqlAuthHost host) => host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static void Authorize(HttpClient client, string accessToken) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    private static async Task<JsonElement> Data(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    private static async Task<(long Id, string Email)> RegisterVerified(SqlAuthHost host, HttpClient client)
    {
        var email = UniqueEmail(); var signup = await client.PostAsJsonAsync("/api/auth/register", Signup("Patient", email));
        Assert.Equal(HttpStatusCode.Created, signup.StatusCode); var id = (await Data(signup)).GetProperty("id").GetInt64();
        var query = QueryHelpers.ParseQuery(new Uri(host.Mail.VerificationLinks[email]).Query);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/auth/verify-email?userId={id}&token={Uri.EscapeDataString(query["token"].ToString())}")).StatusCode);
        return (id, email);
    }
    private static async Task<(string AccessToken, string RefreshToken, string Role)> Login(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await Data(response); return (data.GetProperty("accessToken").GetString()!, data.GetProperty("refreshToken").GetString()!, data.GetProperty("user").GetProperty("role").GetString()!);
    }
    private static async Task<string> CreateAdmin(SqlAuthHost host)
    {
        using var scope = host.Services.CreateScope(); var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var email = UniqueEmail(); var user = new User { UserName = email, Email = email, FirstName = "Test", LastName = "Admin", EmailConfirmed = true };
        var created = await manager.CreateAsync(user, Password); Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded); return email;
    }
}

internal sealed class SqlAuthHost(string connectionString) : WebApplicationFactory<Program>
{
    public CapturedTestEmail Mail { get; } = new();
    private Dictionary<string, string?> Settings => new()
    {
        [HostDefaults.EnvironmentKey] = "Testing",
        ["ConnectionStrings:DefaultConnection"] = connectionString,
        ["Jwt:SecretKey"] = "TestOnly_NutriCare_Integration_Signing_Key_123456789_ABCDEF",
        ["Jwt:Issuer"] = "NutriCareHttpTests", ["Jwt:Audience"] = "NutriCareHttpTests",
        ["DataProtection:KeysPath"] = Path.Combine(AppContext.BaseDirectory, ".local", "test-keys"),
        ["FrontendUrl"] = "https://portal.example.test", ["Database:ApplyMigrations"] = "false",
        ["Database:SeedProposedConfiguration"] = "false", ["Bootstrap:Enabled"] = "false",
        ["Notifications:WorkerEnabled"] = "false", ["VideoConsultations:LifecycleWorkerEnabled"] = "false"
    };

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // DeferredHostBuilder forwards host configuration as entry-point arguments.
        // This runs before Program reads JWT settings, unlike ConfigureAppConfiguration.
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(Settings));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<NutriCareDbContext>>();
            services.AddDbContext<NutriCareDbContext>(options => options.UseSqlServer(connectionString));
            services.RemoveAll<IEmailService>(); services.AddSingleton<IEmailService>(Mail);
            foreach (var worker in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Namespace?.StartsWith("NutriCare.", StringComparison.Ordinal) == true).ToArray()) services.Remove(worker);
        });
    }
}

internal sealed class CapturedTestEmail : IEmailService
{
    public ConcurrentDictionary<string, string> VerificationLinks { get; } = new();
    public ConcurrentDictionary<string, string> ResetLinks { get; } = new();
    public Task<bool> SendEmailAsync(string email, string subject, string message) => Task.FromResult(true);
    public Task<bool> SendVerificationEmailAsync(string email, string verificationLink) { VerificationLinks[email] = verificationLink; return Task.FromResult(true); }
    public Task<bool> SendPasswordResetEmailAsync(string email, string resetLink) { ResetLinks[email] = resetLink; return Task.FromResult(true); }
}
