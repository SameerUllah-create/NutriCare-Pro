namespace NutriCare.Persistence.Repositories;

using System.Data;
using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

public sealed class AppointmentReservationLock(NutriCareDbContext context) : IAppointmentReservationLock
{
    public async Task<T> ExecuteAsync<T>(long nutritionistId, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction != null)
        {
            await AcquireAsync(nutritionistId, cancellationToken);
            var nestedResult = await action();
            await context.SaveChangesAsync(cancellationToken);
            return nestedResult;
        }
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await AcquireAsync(nutritionistId, cancellationToken);
            var result = await action();
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task AcquireAsync(long nutritionistId, CancellationToken cancellationToken)
    {
        if (!context.Database.IsSqlServer()) return;
        var resource = $"NutriCare:booking:{nutritionistId}";
        await context.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Booking is busy; please retry.', 1;", cancellationToken);
    }
}
