using Microsoft.AspNetCore.SignalR;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Features.Realtime;

namespace TicketSystem.Api.Realtime;

/// <summary>
/// SignalR broadcaster. Called from write handlers AFTER SaveChanges. Broadcast
/// failures are swallowed with a warning log — the DB write has committed, we must
/// not throw. Intentionally passes CancellationToken.None to SendAsync: if the
/// originating HTTP request was cancelled, OTHER connected clients still need the
/// broadcast.
/// </summary>
public sealed class TicketBroadcaster : ITicketBroadcaster
{
    private readonly IHubContext<TicketsHub> _hub;
    private readonly ILogger<TicketBroadcaster> _logger;

    public TicketBroadcaster(IHubContext<TicketsHub> hub, ILogger<TicketBroadcaster> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task TicketCreatedAsync(Guid ticketId, Guid customerId, Guid? assignedAgentId, CancellationToken ct = default)
    {
        var evt = new TicketCreatedEvent(ticketId, customerId, assignedAgentId);
        return SafeBroadcast(async () =>
        {
            await _hub.Clients.Group($"ticket-{ticketId}").SendAsync("TicketCreated", evt);
            await _hub.Clients.Group($"customer-{customerId}").SendAsync("TicketCreated", evt);
            if (assignedAgentId.HasValue)
                await _hub.Clients.Group($"agent-{assignedAgentId.Value}").SendAsync("TicketCreated", evt);
            await _hub.Clients.Group("admins").SendAsync("TicketCreated", evt);
        });
    }

    public Task TicketUpdatedAsync(Guid ticketId, string changeKind, Guid customerId, Guid? assignedAgentId, CancellationToken ct = default)
    {
        var evt = new TicketUpdatedEvent(ticketId, changeKind, customerId, assignedAgentId);
        return SafeBroadcast(async () =>
        {
            await _hub.Clients.Group($"ticket-{ticketId}").SendAsync("TicketUpdated", evt);
            await _hub.Clients.Group($"customer-{customerId}").SendAsync("TicketUpdated", evt);
            if (assignedAgentId.HasValue)
                await _hub.Clients.Group($"agent-{assignedAgentId.Value}").SendAsync("TicketUpdated", evt);
            await _hub.Clients.Group("admins").SendAsync("TicketUpdated", evt);
        });
    }

    public Task TicketAssignedAsync(Guid ticketId, Guid? oldAssigneeId, Guid? newAssigneeId, Guid customerId, CancellationToken ct = default)
    {
        var evt = new TicketAssignedEvent(ticketId, oldAssigneeId, newAssigneeId, customerId);
        return SafeBroadcast(async () =>
        {
            await _hub.Clients.Group($"ticket-{ticketId}").SendAsync("TicketAssigned", evt);
            await _hub.Clients.Group($"customer-{customerId}").SendAsync("TicketAssigned", evt);
            // Old assignee needs to remove the row from their list; new assignee needs to add it.
            if (oldAssigneeId.HasValue)
                await _hub.Clients.Group($"agent-{oldAssigneeId.Value}").SendAsync("TicketAssigned", evt);
            if (newAssigneeId.HasValue && newAssigneeId != oldAssigneeId)
                await _hub.Clients.Group($"agent-{newAssigneeId.Value}").SendAsync("TicketAssigned", evt);
            await _hub.Clients.Group("admins").SendAsync("TicketAssigned", evt);
        });
    }

    public Task CommentAddedAsync(Guid ticketId, Guid commentId, CancellationToken ct = default)
    {
        var evt = new CommentAddedEvent(ticketId, commentId);
        // Only the detail-page group cares about comments; list rows don't render them.
        return SafeBroadcast(() =>
            _hub.Clients.Group($"ticket-{ticketId}").SendAsync("CommentAdded", evt));
    }

    public Task TimeEntryLoggedAsync(Guid ticketId, Guid timeEntryId, CancellationToken ct = default)
    {
        var evt = new TimeEntryLoggedEvent(ticketId, timeEntryId);
        return SafeBroadcast(() =>
            _hub.Clients.Group($"ticket-{ticketId}").SendAsync("TimeEntryLogged", evt));
    }

    private async Task SafeBroadcast(Func<Task> broadcast)
    {
        try { await broadcast(); }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR broadcast failed."); }
    }
}
