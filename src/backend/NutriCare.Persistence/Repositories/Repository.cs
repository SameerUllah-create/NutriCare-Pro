namespace NutriCare.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

/// <summary>
/// Generic repository implementation
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly NutriCareDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(NutriCareDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(long id)
    {
        return await _dbSet.FindAsync(id);
    }

    public Task ReloadAsync(T entity) => _context.Entry(entity).ReloadAsync();

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<IEnumerable<T>> GetAsync(Func<IQueryable<T>, IQueryable<T>>? predicate = null)
    {
        IQueryable<T> query = _dbSet;

        if (predicate != null)
        {
            query = predicate(query);
        }

        return await query.ToListAsync();
    }

    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return await Task.FromResult(entity);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null)
            return false;

        _dbSet.Remove(entity);
        return true;
    }

    public async Task<bool> ExistsAsync(long id)
    {
        var entity = await GetByIdAsync(id);
        return entity != null;
    }
}
