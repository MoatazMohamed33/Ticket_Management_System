using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Tickets;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.Tickets.Commands.AssignTicket;

public sealed class AssignTicketCommandHandler : IRequestHandler<AssignTicketCommand, TicketDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUserSummaryQueries _userSummaries;
    private readonly IDashboardCacheInvalidator _dashboardCache;
    private readonly ITicketBroadcaster _broadcaster;
    private readonly ILogger<AssignTicketCommandHandler> _logger;

    public AssignTicketCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        IUserSummaryQueries userSummaries,
        IDashboardCacheInvalidator dashboardCache,
        ITicketBroadcaster broadcaster,
        ILogger<AssignTicketCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _clock = clock;
        _userSummaries = userSummaries;
        _dashboardCache = dashboardCache;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<TicketDto> Handle(AssignTicketCommand cmd, CancellationToken ct)
    {
        var ticket = await _uow.Repository<Ticket>()
            .FirstOrDefaultAsync(t => t.Id == cmd.TicketId, ct);
        if (ticket is null) throw new NotFoundException("Ticket not found.");

        // Controller enforces [Authorize(Roles = "Admin")]; no TicketAccess check.
        if (_currentUser.UserId is not Guid callerId)
            throw new UnauthorizedAccessException();

        // Validate assignee (active SupportAgent). Same generic message for missing /
        // wrong-role / inactive — no user-existence leak (mirrors FR21 no-leak convention).
        if (cmd.AssignedAgentId.HasValue)
        {
            var agent = await _uow.Repository<User>()
                .FirstOrDefaultNoTrackingAsync(u => u.Id == cmd.AssignedAgentId.Value, ct);
            if (agent is null || agent.Role != UserRole.SupportAgent || !agent.IsActive)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(
                        nameof(cmd.AssignedAgentId),
                        "Target user is not an active Support Agent."),
                });
            }
        }

        var clientBytes = Convert.FromBase64String(cmd.RowVersion);
        _uow.SetOriginalConcurrencyToken(ticket, nameof(Ticket.RowVersion), clientBytes);

        var oldAssignee = ticket.AssignedAgentId;
        ticket.Assign(cmd.AssignedAgentId, _clock.UtcNow);

        if (oldAssignee != cmd.AssignedAgentId)
        {
            // Compose activity summary — fetch display names as needed. Use existing
            // ActivityEventType enum (no schema change): AgentAssigned for both first-assign
            // and reassign; Summary text carries the distinction. AgentUnassigned for null.
            var oldSummary = oldAssignee.HasValue
                ? await _userSummaries.GetByIdAsync(oldAssignee.Value, ct)
                : null;
            var newSummary = cmd.AssignedAgentId.HasValue
                ? await _userSummaries.GetByIdAsync(cmd.AssignedAgentId.Value, ct)
                : null;

            ActivityEventType eventType;
            string summary;
            if (oldAssignee is null && cmd.AssignedAgentId is not null)
            {
                eventType = ActivityEventType.AgentAssigned;
                summary = $"Assigned to {newSummary?.DisplayName ?? "agent"}";
            }
            else if (cmd.AssignedAgentId is null)
            {
                eventType = ActivityEventType.AgentUnassigned;
                summary = $"Unassigned (was {oldSummary?.DisplayName ?? "agent"})";
            }
            else
            {
                eventType = ActivityEventType.AgentAssigned;   // reassign is a special case of assign
                summary = $"Reassigned from {oldSummary?.DisplayName ?? "agent"} to {newSummary?.DisplayName ?? "agent"}";
            }

            var activity = ActivityTimelineEntry.Create(
                ticket.Id, callerId, eventType, summary, _clock.UtcNow);
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

        // Story 6.4 — assign affects agentWorkload for both old + new assignee.
        _dashboardCache.Invalidate();

        // Story 7.1 — broadcast to both old + new assignee's agent groups so their lists
        // gain/lose the row; and to ticket-{id} for detail viewers.
        await _broadcaster.TicketAssignedAsync(
            ticket.Id, oldAssignee, cmd.AssignedAgentId, ticket.CustomerId, ct);

        _logger.LogInformation(
            "Admin {AdminId} assigned ticket {TicketId} from {OldAssignee} to {NewAssignee}.",
            callerId, ticket.Id, oldAssignee, cmd.AssignedAgentId);

        return ticket.ToDto();
    }
}
