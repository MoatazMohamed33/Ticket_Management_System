using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Common.Exceptions;

namespace TicketSystem.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public UnitOfWork(AppDbContext db) => _db = db;

    public IGenericRepository<T> Repository<T>() where T : class =>
        (IGenericRepository<T>)_repositories.GetOrAdd(typeof(T), _ => new GenericRepository<T>(_db));

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public void SetOriginalConcurrencyToken<T>(T entity, string propertyName, byte[] originalValue)
        where T : class =>
        _db.Entry(entity).Property(propertyName).OriginalValue = originalValue;

    public async Task<int> SaveChangesWithConcurrencyCheckAsync(
        Func<CancellationToken, Task<IReadOnlyDictionary<string, object?>>> fetchCurrentState,
        CancellationToken ct = default)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var current = await fetchCurrentState(ct);
            throw new ConcurrencyConflictException(current);
        }
    }
}
