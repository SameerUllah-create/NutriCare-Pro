namespace NutriCare.API.BackgroundServices;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NutriCare.API.Hubs;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

/// <summary>Lease-based, bounded-retry outbox delivery; acceptance is not proof of external delivery.</summary>
public sealed class NotificationDeliveryWorker(IServiceScopeFactory scopes, ILogger<NotificationDeliveryWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Notifications:WorkerEnabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Notification worker cycle failed ({ExceptionType})", ex.GetType().Name); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<INotificationOutbox>();
            var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unit.BeginTransactionAsync("notifications:reminders");
            var now = DateTime.UtcNow;
            var appointments = await db.Appointments.Where(a => a.DeletedAt == null && (a.Status == "Confirmed" || a.Status == "Approved") && a.AppointmentDateTime > now && a.AppointmentDateTime <= now.AddHours(24))
                .Include(a => a.Patient).ThenInclude(p => p!.User).Include(a => a.Nutritionist).ThenInclude(n => n!.User).ToListAsync(cancellationToken);
            foreach (var appointment in appointments)
            {
                var bucket = appointment.AppointmentDateTime <= now.AddHours(1) ? "1h" : "24h";
                foreach (var user in new[] { appointment.Patient?.User, appointment.Nutritionist?.User }.Where(u => u != null).Cast<User>())
                    await outbox.EnqueueAsync(new NotificationRecipient(user.Id, user.Email ?? "", user.PhoneNumber, ""),
                        new NotificationRequest("AppointmentReminder", "Appointment reminder", $"Your consultation is scheduled for {appointment.AppointmentDateTime.AddHours(5):dd MMM yyyy h:mm tt} Pakistan time.", "Appointment", appointment.Id,
                            user.Id == appointment.Patient?.UserId ? "/patient/appointments" : "/nutritionist/appointments", ["InApp", "Email"]),
                        $"appointment:{appointment.Id}:reminder:{bucket}:{appointment.AppointmentDateTime:yyyyMMddHHmm}", cancellationToken);
            }
            await unit.CommitAsync();
        }

        long[] ids;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
            var now = DateTime.UtcNow;
            ids = await db.Notifications.Where(n => n.DeletedAt == null && (n.Status == "Pending" || n.Status == "Retry" || n.Status == "Sending") && n.NextAttemptAt <= now)
                .OrderBy(n => n.NextAttemptAt).Select(n => n.Id).Take(50).ToArrayAsync(cancellationToken);
        }
        foreach (var id in ids) await DeliverAsync(id, cancellationToken);
    }

    private async Task DeliverAsync(long id, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
        var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unit.BeginTransactionAsync($"notification:{id}");
        var item = await db.Notifications.Include(n => n.User).SingleOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (item == null || item.DeletedAt != null || item.NextAttemptAt > DateTime.UtcNow || item.Status is not ("Pending" or "Retry" or "Sending"))
        {
            await unit.RollbackAsync(); return;
        }
        var user = item.User;
        if (user is not { IsActive: true, DeletedAt: null } || (item.Channel == "Email" && !user.EmailNotificationsEnabled) ||
            (item.Channel == "SMS" && !user.SmsNotificationsEnabled) || (item.Channel == "InApp" && !user.InAppNotificationsEnabled))
        {
            item.Status = "Skipped"; item.DeliveryStatus = "Skipped"; item.NextAttemptAt = null;
            await unit.CommitAsync(); return;
        }
        if (item.Type == "AppointmentReminder" && item.RelatedToId.HasValue && !await db.Appointments.AnyAsync(a => a.Id == item.RelatedToId && a.DeletedAt == null && (a.Status == "Confirmed" || a.Status == "Approved") && a.AppointmentDateTime > DateTime.UtcNow, cancellationToken))
        {
            item.Status = "Skipped"; item.DeliveryStatus = "Skipped"; item.NextAttemptAt = null;
            await unit.CommitAsync(); return;
        }
        item.Status = "Sending"; item.NextAttemptAt = DateTime.UtcNow.AddMinutes(5); item.AttemptCount++;
        await unit.CommitAsync();
        var success = false;
        try
        {
            var externalMessage = "You have an update in NutriCare Pro. Sign in to review it securely.";
            if (item.Type == "NewMessage") externalMessage = "You have a new secure message in NutriCare Pro. Sign in to read it.";
            if (item.Type == "AppointmentReminder") externalMessage = "You have an upcoming NutriCare Pro consultation. Sign in to view its time and details.";
            success = item.Channel switch
            {
                "Email" => await scope.ServiceProvider.GetRequiredService<IEmailService>().SendEmailAsync(user.Email ?? "", "NutriCare Pro update", externalMessage),
                "SMS" => await scope.ServiceProvider.GetRequiredService<ISmsService>().SendSmsAsync(user.PhoneNumber ?? "", externalMessage, cancellationToken),
                "InApp" => true,
                _ => false
            };
            if (item.Channel == "InApp")
                await scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>().Clients.User(user.Id.ToString()).SendAsync("ReceiveNotification", new { item.Id, item.Type, item.Title, item.Message, item.ActionUrl, item.CreatedAt }, cancellationToken);
        }
        catch (Exception ex) { logger.LogWarning("Notification {NotificationId} attempt {Attempt} failed ({ExceptionType})", id, item.AttemptCount, ex.GetType().Name); }
        await unit.BeginTransactionAsync($"notification:{id}");
        item.Status = success ? "Sent" : item.AttemptCount >= 5 ? "Failed" : "Retry";
        item.DeliveryStatus = success ? item.Channel == "InApp" ? "Available" : "Accepted" : item.Status;
        item.SentAt = success ? DateTime.UtcNow : null;
        item.LastError = success ? null : "Provider did not accept the notification";
        item.NextAttemptAt = success || item.Status == "Failed" ? null : DateTime.UtcNow.AddMinutes(Math.Pow(2, item.AttemptCount));
        await unit.CommitAsync();
    }
}
