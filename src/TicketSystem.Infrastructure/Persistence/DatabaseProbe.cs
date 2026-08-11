using TicketSystem.Application.Abstractions.Persistence;

namespace TicketSystem.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IDatabaseProbe"/>. Uses <c>Database.CanConnectAsync</c>
/// which opens/closes a connection without touching any table — safe even before any migrations run.
/// </summary>
public class DatabaseProbe : IDatabaseProbe
{
    private readonly AppDbContext _db;

    public DatabaseProbe(AppDbContext db) => _db = db;

    public Task<bool> CanConnectAsync(CancellationToken ct = default) => _db.Database.CanConnectAsync(ct);
}
