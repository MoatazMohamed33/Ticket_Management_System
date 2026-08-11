namespace TicketSystem.Application.Features.Realtime;

/// <summary>
/// Small typed events broadcast by the SignalR hub. Deliberately minimal — no display
/// fields; clients refetch the authoritative REST endpoint on any event. Reduces coupling
/// between write handlers and client render shape.
/// </summary>
public sealed record TicketCreatedEvent(Guid TicketId, Guid CustomerId, Guid? AssignedAgentId);

public sealed record TicketUpdatedEvent(
    Guid TicketId,
    string ChangeKind,          // "Status" | "Priority" | "Closed"
    Guid CustomerId,
    Guid? AssignedAgentId);

public sealed record TicketAssignedEvent(
    Guid TicketId,
    Guid? OldAssigneeId,
    Guid? NewAssigneeId,
    Guid CustomerId);

public sealed record CommentAddedEvent(Guid TicketId, Guid CommentId);

public sealed record TimeEntryLoggedEvent(Guid TicketId, Guid TimeEntryId);
