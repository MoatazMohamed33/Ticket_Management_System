using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Common;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Queries.ListAllTickets;

public sealed class ListAllTicketsQueryHandler
    : IRequestHandler<ListAllTicketsQuery, PagedResult<TicketListItemDto>>
{
    private readonly IAllTicketsQueries _queries;
    private readonly ILogger<ListAllTicketsQueryHandler> _logger;

    public ListAllTicketsQueryHandler(
        IAllTicketsQueries queries,
        ILogger<ListAllTicketsQueryHandler> logger)
    {
        _queries = queries;
        _logger = logger;
    }

    public Task<PagedResult<TicketListItemDto>> Handle(
        ListAllTicketsQuery q, CancellationToken ct)
    {
        var statuses = EnumCsvParser.ParseEnumList<TicketStatus>(q.Status);
        var priorities = EnumCsvParser.ParseEnumList<TicketPriority>(q.Priority);

        // AssignedAgentId parse order: check "unassigned" FIRST (case-insensitive),
        // then try Guid, then silently drop.
        Guid? assignedAgentId = null;
        bool assignedAgentIsUnassigned = false;
        if (!string.IsNullOrWhiteSpace(q.AssignedAgentId))
        {
            if (string.Equals(q.AssignedAgentId, "unassigned", StringComparison.OrdinalIgnoreCase))
            {
                assignedAgentIsUnassigned = true;
            }
            else if (Guid.TryParse(q.AssignedAgentId, out var parsed))
            {
                assignedAgentId = parsed;
            }
            else
            {
                _logger.LogDebug(
                    "Ignoring malformed assignedAgentId filter: {Value}", q.AssignedAgentId);
            }
        }

        Guid? customerId = null;
        if (!string.IsNullOrWhiteSpace(q.CustomerId))
        {
            if (Guid.TryParse(q.CustomerId, out var parsed))
            {
                customerId = parsed;
            }
            else
            {
                _logger.LogDebug(
                    "Ignoring malformed customerId filter: {Value}", q.CustomerId);
            }
        }

        return _queries.ListAsync(
            q.Page, q.PageSize,
            statuses, priorities,
            assignedAgentId, assignedAgentIsUnassigned,
            customerId,
            q.Search, q.SortBy, q.SortDirection,
            ct);
    }
}
