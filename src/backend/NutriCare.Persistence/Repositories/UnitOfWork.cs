namespace NutriCare.Persistence.Repositories;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using System.Data;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

/// <summary>
/// Unit of Work implementation for transaction management
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly NutriCareDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(NutriCareDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task<bool> BeginTransactionAsync(string? resource = null)
    {
        if (_transaction != null) throw new InvalidOperationException("A transaction is already active");
        _transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (!string.IsNullOrWhiteSpace(resource) && _context.Database.IsSqlServer())
                await _context.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Operation is busy; retry.', 1;");
        }
        catch { await RollbackAsync(); throw; }
        return true;
    }

    public async Task<bool> CommitAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
            if (_transaction != null) await _transaction.CommitAsync();
            return true;
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public async Task<bool> RollbackAsync()
    {
        try
        {
            if (_transaction != null) await _transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            return true;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
