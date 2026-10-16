namespace NutriCare.API.BackgroundServices;

using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

/// <summary>Revokes Zoom meetings after local cancellation/rescheduling before a replacement can overwrite the provider ID.</summary>
public sealed class VideoMeetingLifecycleWorker(IServiceScopeFactory scopes, ILogger<VideoMeetingLifecycleWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("VideoConsultations:LifecycleWorkerEnabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Video lifecycle worker cycle failed ({ExceptionType})", ex.GetType().Name); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        long[] ids;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
            var now = DateTime.UtcNow;
            ids = await db.VideoConsultations.Where(v => v.DeletedAt == null && v.Status == "Cancelled" &&
                (v.ProviderSyncStatus == "Current" || v.ProviderSyncStatus == "Pending" || v.ProviderSyncStatus == "Retry" || v.ProviderSyncStatus == "Processing") &&
                (v.ProviderSyncNextAttemptAt == null || v.ProviderSyncNextAttemptAt <= now))
                .OrderBy(v => v.ProviderSyncNextAttemptAt).ThenBy(v => v.Id).Select(v => v.Id).Take(25).ToArrayAsync(cancellationToken);
        }
        foreach (var id in ids)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
            var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unit.BeginTransactionAsync($"video-lifecycle:{id}");
            var item = await db.VideoConsultations.SingleAsync(v => v.Id == id, cancellationToken);
            if (item.Status != "Cancelled" || item.ProviderSyncStatus is "Revoked" or "Failed" or "ManualActionRequired" || item.ProviderSyncNextAttemptAt > DateTime.UtcNow)
            { await unit.RollbackAsync(); continue; }
            item.ProviderSyncAttempts++;
            item.ProviderSyncStatus = "Processing";
            item.ProviderSyncNextAttemptAt = DateTime.UtcNow.AddMinutes(5);
            await unit.CommitAsync();
            var result = "Retry";
            try
            {
                result = string.IsNullOrWhiteSpace(item.ProviderMeetingId) ? "Revoked" : await scope.ServiceProvider.GetRequiredService<IVideoMeetingLifecycleService>().CancelMeetingAsync(item.Provider, item.ProviderMeetingId, cancellationToken);
            }
            catch (Exception ex) { logger.LogWarning("Video consultation {ConsultationId} cancellation attempt failed ({ExceptionType})", id, ex.GetType().Name); }
            await unit.BeginTransactionAsync($"video-lifecycle:{id}");
            item.ProviderSyncStatus = result == "Retry" && item.ProviderSyncAttempts >= 5 ? "Failed" : result;
            item.ProviderSyncNextAttemptAt = item.ProviderSyncStatus == "Retry" ? DateTime.UtcNow.AddMinutes(Math.Pow(2, item.ProviderSyncAttempts)) : null;
            await unit.CommitAsync();
        }
    }
}
