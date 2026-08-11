using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.CloseTicket;

public sealed class CloseTicketCommandHandler : IRequestHandler<CloseTicketCommand, TicketDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IDashboardCacheInvalidator _dashboardCache;
    private readonly ITicketBroadcaster _broadcaster;
    private readonly ILogger<CloseTicketCommandHandler> _logger;

    public CloseTicketCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        IDashboardCacheInvalidator dashboardCache,
        ITicketBroadcaster broadcaster,
        ILogger<CloseTicketCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _clock = clock;
        _dashboardCache = dashboardCache;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<TicketDto> Handle(CloseTicketCommand cmd, CancellationToken ct)
    {
        var ticket = await _uow.Repository<Ticket>()
            .FirstOrDefaultAsync(t => t.Id == cmd.TicketId, ct);
        if (ticket is null) throw new NotFoundException("Ticket not found.");

        // FR21 defense-in-depth (controller also restricts to Customer role).
        if (!TicketAccess.CanView(ticket.CustomerId, ticket.AssignedAgentId, _currentUser))
            throw new NotFoundException("Ticket not found.");

        if (_currentUser.UserId is not Guid callerId)
            throw new UnauthorizedAccessException();

        // Optimistic concurrency: set the tracked entity's original RowVersion to the
        // caller-supplied token so EF's next SaveChanges compares against it.
        var clientBytes = Convert.FromBase64String(cmd.RowVersion);
        _uow.SetOriginalConcurrencyToken(ticket, nameof(Ticket.RowVersion), clientBytes);

        // Domain method — throws InvalidTicketTransitionException if status != Resolved.
        ticket.Close(_clock.UtcNow);

        // Emit activity for the closure (FR26 partial).
        var activity = ActivityTimelineEntry.Create(
            ticket.Id, callerId, ActivityEventType.TicketClosed,
            "Customer closed the ticket.", _clock.UtcNow);
        await _uow.Repository<ActivityTimelineEntry>().AddAsync(activity, ct);

        await _uow.SaveChangesWithConcurrencyCheckAsync(async _ct =>
        {
            var current = await _uow.Repository<Ticket>()
                .FirstOrDefaultNoTrackingAsync(t => t.Id == cmd.TicketId, _ct);
            if (current is null)
                return new Dictionary<string, object?>();  // ticket vanished — generic 409
            return new Dictionary<string, object?>
            {
                ["currentRowVersion"] = Convert.ToBase64String(current.RowVersion),
                ["currentStatus"]     = current.Status.ToString(),
            };
        }, ct);

        // Story 6.4 — close affects ticketCountsByStatus + averageResolutionMinutes.
        _dashboardCache.Invalidate();

        // Story 7.1 — broadcast the close.
        await _broadcaster.TicketUpdatedAsync(
            ticket.Id, "Closed", ticket.CustomerId, ticket.AssignedAgentId, ct);

        _logger.LogInformation(
            "Customer {CallerId} closed ticket {TicketId}.", callerId, ticket.Id);

        return ticket.ToDto();
    }
}
