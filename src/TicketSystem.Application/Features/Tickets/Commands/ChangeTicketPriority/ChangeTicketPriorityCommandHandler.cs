using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.ChangeTicketPriority;

public sealed class ChangeTicketPriorityCommandHandler
    : IRequestHandler<ChangeTicketPriorityCommand, TicketDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IDashboardCacheInvalidator _dashboardCache;
    private readonly ITicketBroadcaster _broadcaster;
    private readonly ILogger<ChangeTicketPriorityCommandHandler> _logger;

    public ChangeTicketPriorityCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        IDashboardCacheInvalidator dashboardCache,
        ITicketBroadcaster broadcaster,
        ILogger<ChangeTicketPriorityCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _clock = clock;
        _dashboardCache = dashboardCache;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<TicketDto> Handle(ChangeTicketPriorityCommand cmd, CancellationToken ct)
    {
        var ticket = await _uow.Repository<Ticket>()
            .FirstOrDefaultAsync(t => t.Id == cmd.TicketId, ct);
        if (ticket is null) throw new NotFoundException("Ticket not found.");

        // Controller enforces [Authorize(Roles = "Admin")] — no TicketAccess check needed.
        if (_currentUser.UserId is not Guid callerId)
            throw new UnauthorizedAccessException();

        var clientBytes = Convert.FromBase64String(cmd.RowVersion);
        _uow.SetOriginalConcurrencyToken(ticket, nameof(Ticket.RowVersion), clientBytes);

        var oldPriority = ticket.Priority;
        // Domain method — idempotent same-priority no-op.
        ticket.ChangePriority(cmd.Priority, _clock.UtcNow);

        if (oldPriority != cmd.Priority)
        {
            var activity = ActivityTimelineEntry.Create(
                ticket.Id, callerId, ActivityEventType.PriorityChanged,
                $"Priority: {oldPriority} → {cmd.Priority}", _clock.UtcNow);
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

        // Story 6.4 — priority change affects openCriticalCount when → Critical or ← Critical.
        _dashboardCache.Invalidate();

        // Story 7.1 — broadcast the priority change.
        await _broadcaster.TicketUpdatedAsync(
            ticket.Id, "Priority", ticket.CustomerId, ticket.AssignedAgentId, ct);

        _logger.LogInformation(
            "Admin {AdminId} changed ticket {TicketId} priority from {OldPriority} to {NewPriority}.",
            callerId, ticket.Id, oldPriority, cmd.Priority);

        return ticket.ToDto();
    }
}
