using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.CreateTicket;

public sealed class CreateTicketCommandHandler : IRequestHandler<CreateTicketCommand, TicketDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IDashboardCacheInvalidator _dashboardCache;
    private readonly ITicketBroadcaster _broadcaster;
    private readonly ILogger<CreateTicketCommandHandler> _logger;

    public CreateTicketCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        IDashboardCacheInvalidator dashboardCache,
        ITicketBroadcaster broadcaster,
        ILogger<CreateTicketCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _clock = clock;
        _dashboardCache = dashboardCache;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<TicketDto> Handle(CreateTicketCommand cmd, CancellationToken ct)
    {
        // Defensive — controller [Authorize(Roles="Customer")] should have blocked already.
        if (_currentUser.UserId is not Guid customerId)
        {
            throw new UnauthorizedAccessException();
        }

        var ticket = Ticket.Create(cmd.Title, cmd.Description, cmd.Priority, customerId, _clock.UtcNow);

        await _uow.Repository<Ticket>().AddAsync(ticket, ct);
        await _uow.SaveChangesAsync(ct);

        // Story 6.4 — create affects ticketCountsByStatus (Open increases).
        _dashboardCache.Invalidate();

        // Story 7.1 — SignalR broadcast. Fire-and-forget; SafeBroadcast swallows failures.
        await _broadcaster.TicketCreatedAsync(ticket.Id, customerId, null, ct);

        _logger.LogInformation(
            "Customer {CustomerId} created ticket {TicketId} priority {Priority}.",
            customerId, ticket.Id, ticket.Priority);

        return ticket.ToDto();
    }
}
