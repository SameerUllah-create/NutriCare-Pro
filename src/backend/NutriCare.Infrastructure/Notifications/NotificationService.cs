namespace NutriCare.Infrastructure.Notifications;

using Microsoft.Extensions.Configuration;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;

/// <summary>Durable per-channel outbox. Dispatch results indicate queue acceptance, never provider delivery.</summary>
public class NotificationService(IRepository<Notification> notifications, IRepository<User> users,
    IUnitOfWork unitOfWork, IConfiguration configuration) : INotificationService, INotificationOutbox
{
    private readonly HashSet<string> _staged = new(StringComparer.Ordinal);

    public async Task EnqueueAsync(NotificationRecipient recipient, NotificationRequest request, string eventKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventKey) || eventKey.Length > 240) throw new ArgumentException("A stable event key is required");
        var user = await users.GetByIdAsync(recipient.UserId);
        if (user is not { IsActive: true, DeletedAt: null }) return;
        foreach (var channel in Channels(request))
        {
            if ((channel == "Email" && !user.EmailNotificationsEnabled) || (channel == "SMS" && !user.SmsNotificationsEnabled) || (channel == "InApp" && !user.InAppNotificationsEnabled)) continue;
            var key = $"{recipient.UserId}:{channel}:{eventKey}";
            if (!_staged.Add(key)) continue;
            if ((await notifications.GetAsync(q => q.Where(n => n.UserId == recipient.UserId && n.Channel == channel && n.EventKey == eventKey))).Any()) continue;
            await notifications.AddAsync(new Notification
            {
                UserId = recipient.UserId, Type = request.EventType, Title = request.Title,
                Message = request.Message, RelatedToType = request.RelatedToType, RelatedToId = request.RelatedToId,
                ActionUrl = request.ActionUrl, Channel = channel, EventKey = eventKey,
                Status = "Pending", DeliveryStatus = "Pending", NextAttemptAt = DateTime.UtcNow
            });
        }
    }

    public async Task<IReadOnlyCollection<NotificationDispatchResult>> SendAsync(NotificationRecipient recipient, NotificationRequest request, CancellationToken cancellationToken = default)
    {
        await EnqueueAsync(recipient, request, $"{request.EventType}:{request.RelatedToType}:{request.RelatedToId}", cancellationToken);
        await unitOfWork.SaveChangesAsync();
        return Channels(request).Select(c => new NotificationDispatchResult(recipient.UserId, c, true, null)).ToArray();
    }

    public async Task<IReadOnlyCollection<NotificationDispatchResult>> SendAppointmentBookedAsync(NotificationRecipient patient, NotificationRecipient nutritionist, long appointmentId, DateTime appointmentDateTime, CancellationToken cancellationToken = default)
    {
        var request = new NotificationRequest("AppointmentBooked", "Appointment booked", $"Your consultation is scheduled for {DisplayTime(appointmentDateTime)}.", "Appointment", appointmentId, "/patient/appointments", []);
        var first = await SendAsync(patient, request, cancellationToken);
        var second = await SendAsync(nutritionist, request with { Title = "New appointment", Message = $"A consultation is scheduled for {DisplayTime(appointmentDateTime)}.", ActionUrl = "/nutritionist/appointments" }, cancellationToken);
        return first.Concat(second).ToArray();
    }

    public Task<IReadOnlyCollection<NotificationDispatchResult>> SendAppointmentReminderAsync(NotificationRecipient recipient, long appointmentId, DateTime appointmentDateTime, CancellationToken cancellationToken = default) =>
        SendAsync(recipient, new NotificationRequest("AppointmentReminder", "Appointment reminder", $"Your consultation is scheduled for {DisplayTime(appointmentDateTime)}.", "Appointment", appointmentId, "/patient/appointments", []), cancellationToken);

    public Task<IReadOnlyCollection<NotificationDispatchResult>> SendDietPlanAssignedAsync(NotificationRecipient patient, long dietPlanId, string planTitle, CancellationToken cancellationToken = default) =>
        SendAsync(patient, new NotificationRequest("DietPlanAssigned", "New plan available", "Your nutritionist has published a plan. Sign in to review it securely.", "DietPlan", dietPlanId, "/patient/plans", []), cancellationToken);

    public Task<IReadOnlyCollection<NotificationDispatchResult>> SendPaymentReceivedAsync(NotificationRecipient patient, long paymentId, decimal amount, string currency, CancellationToken cancellationToken = default) =>
        SendAsync(patient, new NotificationRequest("PaymentReceived", "Payment received", $"Your payment of {amount:0.00} {currency} was received.", "Payment", paymentId, "/patient/payments", []), cancellationToken);

    public Task<IReadOnlyCollection<NotificationDispatchResult>> SendNewMessageAsync(NotificationRecipient recipient, long senderId, long messageId, CancellationToken cancellationToken = default) =>
        SendAsync(recipient, new NotificationRequest("NewMessage", "New secure message", "You have a new secure message. Sign in to read it.", "Message", messageId, "/messages", []), cancellationToken);

    private IReadOnlyCollection<string> Channels(NotificationRequest request)
    {
        var configured = request.Channels.Count > 0 ? request.Channels : configuration.GetSection($"Notifications:Events:{request.EventType}:Channels").Get<string[]>() ?? ["InApp", "Email"];
        return configured.Select(c => c.ToLowerInvariant() switch { "email" => "Email", "sms" => "SMS", "inapp" or "in-app" => "InApp", _ => "" }).Where(c => c != "").Distinct().ToArray();
    }

    private static string DisplayTime(DateTime instant) => $"{DateTime.SpecifyKind(instant, DateTimeKind.Utc).AddHours(5):dd MMM yyyy h:mm tt} Pakistan time";
}
