using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Abstractions.Persistence;

/// <summary>
/// Customer-scoped ticket list. Kept out of <see cref="IGenericRepository{T}"/> because
/// it needs EF-specific features (EF.Functions.Like with collation, projection with
/// correlated sub-selects) that don't fit the generic shape.
/// Epic 5 will add IMyAssignedTicketsQueries (agent) and Epic 6 IAllTicketsQueries (admin).
/// </summary>
public interface IMyTicketsQueries
{
    Task<PagedResult<TicketListItemDto>> ListForCustomerAsync(
        Guid customerId,
        int page,
        int pageSize,
        TicketStatus[]? statuses,
        TicketPriority[]? priorities,
        string? search,
        string sortBy,
        string sortDirection,
        CancellationToken ct = default);
}
