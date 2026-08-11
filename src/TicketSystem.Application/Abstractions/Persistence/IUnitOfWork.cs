namespace TicketSystem.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    IGenericRepository<T> Repository<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Set the original value of a concurrency-token property on a tracked entity so
    /// the next SaveChanges uses the client-supplied token for the optimistic-concurrency
    /// check. Called before mutating an entity whose RowVersion came from an HTTP body.
    /// </summary>
    void SetOriginalConcurrencyToken<T>(T entity, string propertyName, byte[] originalValue)
        where T : class;

    /// <summary>
    /// Save pending changes and translate optimistic-concurrency failures into
    /// <see cref="Common.Exceptions.ConcurrencyConflictException"/> populated with the
    /// caller-supplied current state (fetched only on the failure path).
    /// </summary>
    Task<int> SaveChangesWithConcurrencyCheckAsync(
        Func<CancellationToken, Task<IReadOnlyDictionary<string, object?>>> fetchCurrentState,
        CancellationToken ct = default);
}
