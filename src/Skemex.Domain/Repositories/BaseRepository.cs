using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Domain.Repositories;

public class BaseRepository<T>(DbContext dbContext) : IBaseRepository<T> where T : class, IEntity<Guid>
{
    private readonly DbSet<T> _dbSet = dbContext.Set<T>();

    public async Task<List<T>> GetAllAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQuery(cancellationToken);

        query = include == null ? query : include(query);
        query = filter == null ? query : query.Where(filter);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PaginatedList<T>> GetAllPaginatedAsync(
        int pageNumber, int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQuery(cancellationToken);

        query = include == null ? query : include(query);
        query = filter == null ? query : query.Where(filter);

        return await query.PaginateAsync(pageNumber, pageSize, cancellationToken);
    }

    public async Task<List<TResult>> GetAllWithSelectorAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQuery(cancellationToken);

        query = include == null ? query : include(query);
        query = filter == null ? query : query.Where(filter);

        return await query
            .Select(selector)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaginatedList<TResult>> GetAllWithSelectorPaginatedAsync<TResult>(
        int pageNumber, int pageSize,
        Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQuery(cancellationToken);

        query = include == null ? query : include(query);
        query = filter == null ? query : query.Where(filter);

        return await query
            .Select(selector)
            .PaginateAsync(pageNumber, pageSize, cancellationToken);
    }

    public async Task<T?> GetAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQuery(cancellationToken);

        query = include == null ? query : include(query);
        query = filter == null ? query : query.Where(filter);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TResult?> GetWithSelectorAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQuery(cancellationToken);

        query = include == null ? query : include(query);
        query = filter == null ? query : query.Where(filter);

        return await query
            .Select(selector)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await _dbSet.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = _dbSet.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entry.Entity;
    }

    public async Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        _dbSet.UpdateRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var enumerable = entities.ToList();
        _dbSet.RemoveRange(enumerable);
        return await dbContext.SaveChangesAsync(cancellationToken) == enumerable.Count;
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQuery(cancellationToken);

        query = include == null ? query : include(query);
        query = filter == null ? query : query.Where(filter);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQuery(cancellationToken);

        query = include == null ? query : include(query);
        query = filter == null ? query : query.Where(filter);

        return await query.CountAsync(cancellationToken);
    }

    public virtual async Task<IQueryable<T>> GetQuery(CancellationToken cancellationToken = default)
        => await Task.FromResult(_dbSet);

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}