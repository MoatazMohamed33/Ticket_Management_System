namespace TicketSystem.Application.Features.Health.Queries.GetHealth;

/// <summary>Liveness check response. Trivial — does not touch the DB.</summary>
public sealed record HealthResponseDto(string Status, string ApiVersion, DateTimeOffset UtcNow);
