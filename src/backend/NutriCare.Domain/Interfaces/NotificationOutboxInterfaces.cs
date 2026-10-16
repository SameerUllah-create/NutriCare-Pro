namespace NutriCare.Domain.Interfaces;

/// <summary>Stages durable notification rows in the caller's unit of work without committing it.</summary>
public interface INotificationOutbox
{
    Task EnqueueAsync(NotificationRecipient recipient, NotificationRequest request, string eventKey, CancellationToken cancellationToken = default);
}
