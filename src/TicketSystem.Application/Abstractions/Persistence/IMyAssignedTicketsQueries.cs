using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Abstractions.Persistence;

/// <summary>
/// Agent-scoped ticket list. Mirrors <see cref="IMyTicketsQueries"/> but filters on
/// AssignedAgentId. Epic 6 adds an admin equivalent alongside.
/// </summary>
public interface IMyAssignedTicketsQueries
{
    Task<PagedResult<TicketListItemDto>> ListForAgentAsync(
        Guid agentId,
        int page,
        int pageSize,
        TicketStatus[]? statuses,
        TicketPriority[]? priorities,
        string? search,
        string sortBy,
        string sortDirection,
        CancellationToken ct = default);
}
