using TicketSystem.Application.Features.Tickets;

namespace TicketSystem.Application.Abstractions.Persistence;

/// <summary>
/// Small helper query for embedding a user summary into DTOs. Shared by 4.4/4.5 and
/// Epic 5 — every write handler returns a DTO that includes an author/actor summary.
/// </summary>
public interface IUserSummaryQueries
{
    Task<UserSummaryDto?> GetByIdAsync(Guid userId, CancellationToken ct = default);
}
