using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;

namespace TicketSystem.Infrastructure.Persistence;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    private readonly AppDbContext _db;
    private readonly DbSet<T> _set;

    public GenericRepository(AppDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public Task<T?> GetByIdAsync(object id, CancellationToken ct = default) =>
        _set.FindAsync(new[] { id }, ct).AsTask();

    public IQueryable<T> Query() => _set;
    public IQueryable<T> QueryNoTracking() => _set.AsNoTracking();

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.FirstOrDefaultAsync(predicate, ct);

    public Task<T?> FirstOrDefaultNoTrackingAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.AsNoTracking().FirstOrDefaultAsync(predicate, ct);

    public Task<List<T>> ToListNoTrackingAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        (predicate is null ? _set.AsNoTracking() : _set.AsNoTracking().Where(predicate)).ToListAsync(ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.AnyAsync(predicate, ct);

    public Task<long> LongCountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.AsNoTracking().LongCountAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _set.AddAsync(entity, ct);

    public void Update(T entity) => _set.Update(entity);
    public void Remove(T entity) => _set.Remove(entity);
}
