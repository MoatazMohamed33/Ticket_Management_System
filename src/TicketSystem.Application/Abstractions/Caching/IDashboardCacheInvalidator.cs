namespace TicketSystem.Application.Abstractions.Caching;

/// <summary>
/// Cross-story cache-invalidation seam for the admin dashboard. Every ticket-mutation
/// handler that changes any of the dashboard's four metrics calls <see cref="Invalidate"/>
/// after SaveChanges. The versioned-prefix design (see Infrastructure impl) sidesteps
/// IMemoryCache's lack of wildcard eviction — reads always incorporate the current
/// version into the cache key, so bumping the version makes prior entries unreachable.
/// Growth path: swap the in-memory implementation for a Redis-backed distributed version
/// when NFR-C1's second API instance appears; the interface stays the same.
/// </summary>
public interface IDashboardCacheInvalidator
{
    /// <summary>Monotonically-increasing version integer. Handler reads this to build
    /// the current cache key. Interlocked.Increment-safe in the in-memory impl.</summary>
    long CurrentVersion { get; }

    /// <summary>Bump the version — makes all keys built from prior versions unreachable.
    /// Fire-and-forget from write handlers (no exceptions).</summary>
    void Invalidate();
}
