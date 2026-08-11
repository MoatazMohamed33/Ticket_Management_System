using System.Linq.Expressions;

namespace TicketSystem.Application.Abstractions.Persistence;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    IQueryable<T> Query();
    IQueryable<T> QueryNoTracking();

    // Async convenience methods so Application handlers stay free of EF Core.
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> FirstOrDefaultNoTrackingAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<List<T>> ToListNoTrackingAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<long> LongCountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
