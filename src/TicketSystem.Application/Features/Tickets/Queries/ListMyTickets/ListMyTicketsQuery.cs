using MediatR;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;

namespace TicketSystem.Application.Features.Tickets.Queries.ListMyTickets;

/// <summary>
/// Bound from [FromQuery]. Status/Priority arrive as comma-separated strings — parsed in
/// the handler with case-insensitive Enum.TryParse per token. Unknown tokens dropped
/// silently (tolerant of typos, same philosophy as unknown-sortBy in Story 3.2).
/// </summary>
public sealed record ListMyTicketsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Priority = null,
    string? Search = null,
    string SortBy = "updatedAt",
    string SortDirection = "desc") : IRequest<PagedResult<TicketListItemDto>>;
