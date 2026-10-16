namespace NutriCare.API.BackgroundServices;

using Microsoft.EntityFrameworkCore;
using NutriCare.Application.Services;
using NutriCare.Persistence.Contexts;

public sealed class CancellationRefundWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<CancellationRefundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Payments:CancellationRefunds:WorkerEnabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Cancellation refund cycle failed ({ExceptionType})", ex.GetType().Name); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        long[] ids;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>(); var now = DateTime.UtcNow;
            ids = await db.CancellationRefundInstructions.AsNoTracking().Where(i => i.DeletedAt == null && i.Eligible == true &&
                (i.Status == "Queued" || i.Status == "AwaitingSettlement" || i.Status == "AwaitingProvider" || i.Status == "Processing") &&
                (i.NextAttemptAt == null || i.NextAttemptAt <= now) && (i.LeaseExpiresAt == null || i.LeaseExpiresAt <= now))
                .OrderBy(i => i.NextAttemptAt).Select(i => i.Id).Take(25).ToArrayAsync(cancellationToken);
        }
        foreach (var id in ids)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CancellationRefundProcessor>().ProcessAsync(id, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning("Cancellation refund instruction {InstructionId} failed ({ExceptionType})", id, ex.GetType().Name); }
        }
    }
}
