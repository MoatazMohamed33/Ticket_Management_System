using MediatR;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Application.Features.Tickets;

namespace TicketSystem.Application.Features.Tickets.Queries.GetTicketDetail;

public sealed class GetTicketDetailQueryHandler : IRequestHandler<GetTicketDetailQuery, TicketDetailDto>
{
    private readonly ITicketDetailQueries _queries;
    private readonly ICurrentUser _currentUser;

    public GetTicketDetailQueryHandler(ITicketDetailQueries queries, ICurrentUser currentUser)
    {
        _queries = queries;
        _currentUser = currentUser;
    }

    public async Task<TicketDetailDto> Handle(GetTicketDetailQuery q, CancellationToken ct)
    {
        var detail = await _queries.GetAsync(q.TicketId, ct);
        // FR21 — 404 for both missing and unauthorized; body indistinguishable.
        if (detail is null) throw new NotFoundException("Ticket not found.");
        if (!TicketAccess.CanView(detail, _currentUser))
            throw new NotFoundException("Ticket not found.");

        return detail;
    }
}
