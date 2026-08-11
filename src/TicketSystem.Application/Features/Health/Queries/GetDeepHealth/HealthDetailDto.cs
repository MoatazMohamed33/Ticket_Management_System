namespace TicketSystem.Application.Features.Health.Queries.GetDeepHealth;

/// <summary>Detailed health response including database reachability and latency.</summary>
public sealed record HealthDetailDto(
    string ApiVersion,
    bool DatabaseReachable,
    long DbLatencyMs,
    DateTimeOffset UtcNow);
