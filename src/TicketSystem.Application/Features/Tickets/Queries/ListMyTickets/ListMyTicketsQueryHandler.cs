using MediatR;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Queries.ListMyTickets;

public sealed class ListMyTicketsQueryHandler
    : IRequestHandler<ListMyTicketsQuery, PagedResult<TicketListItemDto>>
{
    private readonly IMyTicketsQueries _queries;
    private readonly ICurrentUser _currentUser;

    public ListMyTicketsQueryHandler(IMyTicketsQueries queries, ICurrentUser currentUser)
    {
        _queries = queries;
        _currentUser = currentUser;
    }

    public Task<PagedResult<TicketListItemDto>> Handle(ListMyTicketsQuery q, CancellationToken ct)
    {
        // Defensive — controller [Authorize(Roles="Customer")] should have blocked already.
        if (_currentUser.UserId is not Guid customerId)
        {
            throw new UnauthorizedAccessException();
        }

        var statuses = EnumCsvParser.ParseEnumList<TicketStatus>(q.Status);
        var priorities = EnumCsvParser.ParseEnumList<TicketPriority>(q.Priority);

        return _queries.ListForCustomerAsync(
            customerId,
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
