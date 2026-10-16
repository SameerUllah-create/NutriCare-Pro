namespace NutriCare.Domain.Interfaces;

public interface IVideoMeetingLifecycleService
{
    Task<string> CancelMeetingAsync(string provider, string providerMeetingId, CancellationToken cancellationToken = default);
}
