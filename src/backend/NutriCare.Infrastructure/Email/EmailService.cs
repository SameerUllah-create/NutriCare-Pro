namespace NutriCare.Infrastructure.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NutriCare.Domain.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using System.Text;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IHostEnvironment _environment;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public async Task<bool> SendEmailAsync(string email, string subject, string message)
    {
        try
        {
            var apiKey = _configuration["SendGrid:ApiKey"];
            var fromEmail = _configuration["SendGrid:FromEmail"] ?? "noreply@nutricare.com";
            var fromName = _configuration["SendGrid:FromName"] ?? "NutriCare Pro";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                var directory = _configuration["Integrations:DevelopmentMailDirectory"];
                if (!_environment.IsDevelopment() || string.IsNullOrWhiteSpace(directory)) return false;
                var root = Path.GetFullPath(directory, _environment.ContentRootPath);
                Directory.CreateDirectory(root);
                var recipientHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())))[..16];
                var path = Path.Combine(root, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{recipientHash}-{Guid.NewGuid():N}.eml");
                await File.WriteAllTextAsync(path, $"To: {email.Replace("\r", "").Replace("\n", "")}\nSubject: {subject.Replace("\r", "").Replace("\n", "")}\n\n{message}", Encoding.UTF8);
                _logger.LogInformation("Development email saved to the configured local mailbox");
                return true;
            }

            var client = new SendGridClient(apiKey);
            var mail = MailHelper.CreateSingleEmail(
                new EmailAddress(fromEmail, fromName),
                new EmailAddress(email),
                subject,
                message,
                $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>");

            var response = await client.SendEmailAsync(mail);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Email provider did not accept the message. Status: {StatusCode}", response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError("Email dispatch failed ({ExceptionType})", ex.GetType().Name);
            return false;
        }
    }

    public Task<bool> SendVerificationEmailAsync(string email, string verificationLink)
    {
        var message = $"Please verify your NutriCare Pro email by opening this link: {verificationLink}";
        return SendEmailAsync(email, "Verify your NutriCare Pro email", message);
    }

    public Task<bool> SendPasswordResetEmailAsync(string email, string resetLink)
    {
        var message = $"Reset your NutriCare Pro password by opening this link: {resetLink}";
        return SendEmailAsync(email, "Reset your NutriCare Pro password", message);
    }
}
