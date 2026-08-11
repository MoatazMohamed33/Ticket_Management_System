using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.ChangeTicketStatus;

public sealed class ChangeTicketStatusCommandHandler
    : IRequestHandler<ChangeTicketStatusCommand, TicketDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IDashboardCacheInvalidator _dashboardCache;
    private readonly ITicketBroadcaster _broadcaster;
    private readonly ILogger<ChangeTicketStatusCommandHandler> _logger;

    public ChangeTicketStatusCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        IDashboardCacheInvalidator dashboardCache,
        ITicketBroadcaster broadcaster,
        ILogger<ChangeTicketStatusCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _clock = clock;
        _dashboardCache = dashboardCache;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<TicketDto> Handle(ChangeTicketStatusCommand cmd, CancellationToken ct)
    {
        var ticket = await _uow.Repository<Ticket>()
            .FirstOrDefaultAsync(t => t.Id == cmd.TicketId, ct);
        if (ticket is null) throw new NotFoundException("Ticket not found.");

        // FR22 — Agent-not-assigned → 404 (no leak). Admin always allowed. Customer 403'd by controller.
        if (!TicketAccess.CanMutateStatus(ticket.CustomerId, ticket.AssignedAgentId, _currentUser))
            throw new NotFoundException("Ticket not found.");

        if (_currentUser.UserId is not Guid callerId)
            throw new UnauthorizedAccessException();

        var clientBytes = Convert.FromBase64String(cmd.RowVersion);
        _uow.SetOriginalConcurrencyToken(ticket, nameof(Ticket.RowVersion), clientBytes);

        var oldStatus = ticket.Status;
        // Domain method — throws InvalidTicketTransitionException for illegal transitions.
        // Idempotent same-status no-op returns without state change or activity emission.
        ticket.ChangeStatus(cmd.Status, _clock.UtcNow);

        if (oldStatus != cmd.Status)
        {
            var activity = ActivityTimelineEntry.Create(
                ticket.Id, callerId, ActivityEventType.StatusChanged,
                $"Status: {oldStatus} → {cmd.Status}", _clock.UtcNow);
            await _uow.Repository<ActivityTimelineEntry>().AddAsync(activity, ct);
        }

        await _uow.SaveChangesWithConcurrencyCheckAsync(async _ct =>
        {
            var current = await _uow.Repository<Ticket>()
                .FirstOrDefaultNoTrackingAsync(t => t.Id == cmd.TicketId, _ct);
            if (current is null) return new Dictionary<string, object?>();
            return new Dictionary<string, object?>
            {
                ["currentRowVersion"] = Convert.ToBase64String(current.RowVersion),
                ["currentStatus"]     = current.Status.ToString(),
            };
        }, ct);

        // Story 6.4 — status change affects ticketCountsByStatus + agentWorkload +
        // (on transition to Closed) averageResolutionMinutes.
        _dashboardCache.Invalidate();

        // Story 7.1 — broadcast the update (or the "Closed" transition specifically).
        var changeKind = cmd.Status == TicketStatus.Closed ? "Closed" : "Status";
        await _broadcaster.TicketUpdatedAsync(
            ticket.Id, changeKind, ticket.CustomerId, ticket.AssignedAgentId, ct);

        _logger.LogInformation(
            "User {CallerId} changed ticket {TicketId} status from {OldStatus} to {NewStatus}.",
            callerId, ticket.Id, oldStatus, cmd.Status);

        return ticket.ToDto();
    }
}
