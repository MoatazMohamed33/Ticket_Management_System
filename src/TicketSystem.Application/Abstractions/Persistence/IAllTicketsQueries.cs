using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Abstractions.Persistence;

/// <summary>
/// Admin-scoped ticket list (FR14, FR23). No ownership filter — Admin sees everything.
/// Two extra filters compared to <see cref="IMyTicketsQueries"/> /
/// <see cref="IMyAssignedTicketsQueries"/>: caller-supplied assignedAgentId + customerId.
/// The <c>assignedAgentIsUnassigned</c> flag lets the caller distinguish "filter omitted"
/// from "filter is unassigned" — nullable-Guid alone cannot.
/// </summary>
public interface IAllTicketsQueries
{
    Task<PagedResult<TicketListItemDto>> ListAsync(
        int page,
        int pageSize,
        TicketStatus[]? statuses,
        TicketPriority[]? priorities,
        Guid? assignedAgentId,
        bool assignedAgentIsUnassigned,
        Guid? customerId,
        string? search,
        string sortBy,
        string sortDirection,
        CancellationToken ct = default);
}
