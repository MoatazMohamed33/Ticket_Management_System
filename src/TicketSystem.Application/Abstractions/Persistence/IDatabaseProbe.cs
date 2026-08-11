namespace TicketSystem.Application.Abstractions.Persistence;

/// <summary>
/// Lightweight database reachability check used by the deep health endpoint.
/// Implemented in Infrastructure so Application stays free of EF Core.
/// </summary>
public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken ct = default);
}
