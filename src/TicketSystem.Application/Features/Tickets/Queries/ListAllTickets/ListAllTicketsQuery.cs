using MediatR;
using TicketSystem.Application.Common.Paging;

namespace TicketSystem.Application.Features.Tickets.Queries.ListAllTickets;

/// <summary>
/// Admin-scoped list query (FR14, FR23). AssignedAgentId/CustomerId are strings (not Guids)
/// so the caller can pass the magic value "unassigned" for the null-assignee case.
/// </summary>
public sealed record ListAllTicketsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Priority = null,
    string? AssignedAgentId = null,
    string? CustomerId = null,
    string? Search = null,
    string SortBy = "updatedAt",
    string SortDirection = "desc") : IRequest<PagedResult<Features.Tickets.TicketListItemDto>>;
