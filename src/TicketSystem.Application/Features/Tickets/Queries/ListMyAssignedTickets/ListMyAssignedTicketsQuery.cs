using MediatR;
using TicketSystem.Application.Common.Paging;

namespace TicketSystem.Application.Features.Tickets.Queries.ListMyAssignedTickets;

/// <summary>
/// Bound from [FromQuery]. Default sort is priority DESC then updatedAt DESC per PRD
/// Journey 2 (Rana the Agent) — Criticals first, most-recently-touched within priority.
/// </summary>
public sealed record ListMyAssignedTicketsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Priority = null,
    string? Search = null,
    string SortBy = "priority",
    string SortDirection = "desc") : IRequest<PagedResult<TicketListItemDto>>;
