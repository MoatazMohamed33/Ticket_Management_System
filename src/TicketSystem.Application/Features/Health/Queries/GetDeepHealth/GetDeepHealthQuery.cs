using MediatR;

namespace TicketSystem.Application.Features.Health.Queries.GetDeepHealth;

/// <summary>Query that returns detailed health, including a DB round-trip check.</summary>
public sealed record GetDeepHealthQuery : IRequest<HealthDetailDto>;
