using Microsoft.EntityFrameworkCore;
using Skemex.Domain.Abstractions;

namespace Skemex.Domain.Repositories;

public static class QueryableExtensions
{
    extension<T>(IQueryable<T> queryable)
    {
        private async Task<PaginatedList<T>> CreateAsync(
            int page, int pageSize,
            CancellationToken cancellationToken = default)
        {
            var totalItems = await queryable.CountAsync(cancellationToken);
            var items = await queryable
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PaginatedList<T>(items, totalItems, page, pageSize);
        }

        public Task<PaginatedList<T>> PaginateAsync(
            int page, int pageSize,
            CancellationToken cancellationToken = default)
            => queryable.CreateAsync(page, pageSize, cancellationToken);
    }
}