namespace TicketSystem.Domain.Tickets;

public enum ActivityEventType
{
    TicketCreated = 1,
    StatusChanged = 2,
    PriorityChanged = 3,
    AgentAssigned = 4,
    AgentUnassigned = 5,
    TicketClosed = 6,
}

public sealed class ActivityTimelineEntry
{
    private ActivityTimelineEntry() { }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public ActivityEventType Event { get; private set; }
    // Pre-rendered text — frozen at emit time so a later User.DisplayName rename doesn't
    // silently rewrite history. Actor id stored separately for filtering.
    public string Summary { get; private set; } = "";
    public DateTimeOffset At { get; private set; }

    public static ActivityTimelineEntry Create(
        Guid ticketId,
        Guid actorUserId,
        ActivityEventType @event,
        string summary,
        DateTimeOffset now)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("TicketId is required.", nameof(ticketId));
        return new ActivityTimelineEntry
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ActorUserId = actorUserId,
            Event = @event,
            Summary = summary ?? "",
            At = now,
        };
    }
}
