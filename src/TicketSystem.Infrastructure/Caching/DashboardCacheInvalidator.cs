using TicketSystem.Application.Abstractions.Caching;

namespace TicketSystem.Infrastructure.Caching;

/// <summary>
/// In-memory versioned-prefix invalidator. Registered as a singleton so all callers
/// share the version counter. Distributed-cache growth path: replace with a
/// Redis-backed version + pub/sub bump notification — the interface is stable.
/// </summary>
public sealed class DashboardCacheInvalidator : IDashboardCacheInvalidator
{
    private long _version;

    public long CurrentVersion => Interlocked.Read(ref _version);

    public void Invalidate() => Interlocked.Increment(ref _version);
}
