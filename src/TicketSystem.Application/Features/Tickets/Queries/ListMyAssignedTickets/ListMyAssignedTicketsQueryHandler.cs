using MediatR;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Queries.ListMyAssignedTickets;

public sealed class ListMyAssignedTicketsQueryHandler
    : IRequestHandler<ListMyAssignedTicketsQuery, PagedResult<TicketListItemDto>>
{
    private readonly IMyAssignedTicketsQueries _queries;
    private readonly ICurrentUser _currentUser;

    public ListMyAssignedTicketsQueryHandler(
        IMyAssignedTicketsQueries queries,
        ICurrentUser currentUser)
    {
        _queries = queries;
        _currentUser = currentUser;
    }

    public Task<PagedResult<TicketListItemDto>> Handle(
        ListMyAssignedTicketsQuery q, CancellationToken ct)
    {
        // Defensive — controller [Authorize(Roles="SupportAgent")] should have blocked already.
        if (_currentUser.UserId is not Guid agentId)
        {
            throw new UnauthorizedAccessException();
        }

        var statuses = EnumCsvParser.ParseEnumList<TicketStatus>(q.Status);
        var priorities = EnumCsvParser.ParseEnumList<TicketPriority>(q.Priority);

        return _queries.ListForAgentAsync(
            agentId,
            q.Page,
            q.PageSize,
            statuses,
            priorities,
            q.Search,
            q.SortBy,
            q.SortDirection,
            ct);
    }
}
