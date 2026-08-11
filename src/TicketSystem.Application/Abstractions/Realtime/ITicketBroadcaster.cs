namespace TicketSystem.Application.Abstractions.Realtime;

/// <summary>
/// Fire-and-forget SignalR broadcasts. Called AFTER SaveChanges in each ticket-write
/// handler. Never throws — broadcast failure must NOT roll back a committed DB write.
/// Implementation lives in Api layer (needs IHubContext); same seam pattern as
/// IDashboardCacheInvalidator (Story 6.4).
/// </summary>
public interface ITicketBroadcaster
{
    Task TicketCreatedAsync(Guid ticketId, Guid customerId, Guid? assignedAgentId, CancellationToken ct = default);

    Task TicketUpdatedAsync(Guid ticketId, string changeKind, Guid customerId, Guid? assignedAgentId, CancellationToken ct = default);

    Task TicketAssignedAsync(Guid ticketId, Guid? oldAssigneeId, Guid? newAssigneeId, Guid customerId, CancellationToken ct = default);

    Task CommentAddedAsync(Guid ticketId, Guid commentId, CancellationToken ct = default);

    Task TimeEntryLoggedAsync(Guid ticketId, Guid timeEntryId, CancellationToken ct = default);
}
