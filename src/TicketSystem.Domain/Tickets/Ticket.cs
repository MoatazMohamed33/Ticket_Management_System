namespace TicketSystem.Domain.Tickets;

public sealed class Ticket
{
    // EF ctor
    private Ticket() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? AssignedAgentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }   // Story 6.4 — powers averageResolutionMinutes
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>Bump UpdatedAt so recent-activity ordering reflects the touch.
    /// Called on comment adds, status/priority/agent changes, and close events.</summary>
    public void TouchUpdatedAt(DateTimeOffset now) => UpdatedAt = now;

    // Single source of truth for the ticket state machine (Story 5.2). Every mutation
    // path (Close, ChangeStatus, future admin overrides) resolves through this set.
    private static readonly HashSet<(TicketStatus From, TicketStatus To)> LegalTransitions = new()
    {
        (TicketStatus.Open,       TicketStatus.InProgress),
        (TicketStatus.InProgress, TicketStatus.Resolved),
        (TicketStatus.InProgress, TicketStatus.Open),
        (TicketStatus.Resolved,   TicketStatus.InProgress),
        (TicketStatus.Resolved,   TicketStatus.Closed),
    };

    /// <summary>
    /// Terminal state transition (customer closes a Resolved ticket, FR19). Idempotent
    /// on already-Closed; throws <see cref="InvalidTicketTransitionException"/> for any
    /// other current status. Sets <see cref="ClosedAt"/> for dashboard resolution metrics.
    /// </summary>
    public void Close(DateTimeOffset now)
    {
        if (Status == TicketStatus.Closed) return;
        if (Status != TicketStatus.Resolved)
            throw new InvalidTicketTransitionException(Status, TicketStatus.Closed);

        Status = TicketStatus.Closed;
        UpdatedAt = now;
        ClosedAt = now;
    }

    /// <summary>
    /// Generic status change guarded by <see cref="LegalTransitions"/>. Same-status is
    /// an idempotent no-op (no throw, no UpdatedAt bump). Used by Story 5.2's
    /// PATCH /status endpoint for Agent + Admin transitions. Sets <see cref="ClosedAt"/>
    /// on transition to Closed (Story 6.4); does not clear it on other transitions
    /// (LegalTransitions makes Closed terminal anyway).
    /// </summary>
    public void ChangeStatus(TicketStatus newStatus, DateTimeOffset now)
    {
        if (Status == newStatus) return;
        if (!LegalTransitions.Contains((Status, newStatus)))
            throw new InvalidTicketTransitionException(Status, newStatus);

        Status = newStatus;
        UpdatedAt = now;
        if (newStatus == TicketStatus.Closed) ClosedAt = now;
    }

    /// <summary>
    /// Change the priority (FR17). Idempotent on same-priority. No state-machine guard —
    /// priority is unconstrained. Used by Story 6.2's PATCH /priority endpoint (Admin-only).
    /// </summary>
    public void ChangePriority(TicketPriority newPriority, DateTimeOffset now)
    {
        if (Priority == newPriority) return;
        Priority = newPriority;
        UpdatedAt = now;
    }

    /// <summary>
    /// Assign, reassign, or unassign the ticket (FR18). Idempotent when the new assignee
    /// equals the current (both-null counts as equal). The Assigned/Reassigned/Unassigned
    /// activity emission is the handler's responsibility since it depends on the old/new pair.
    /// </summary>
    public void Assign(Guid? newAssignee, DateTimeOffset now)
    {
        if (AssignedAgentId == newAssignee) return;
        AssignedAgentId = newAssignee;
        UpdatedAt = now;
    }

    public static Ticket Create(
        string title,
        string description,
        TicketPriority priority,
        Guid customerId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));

        return new Ticket
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = description.Trim(),
            Priority = priority,
            Status = TicketStatus.Open,      // FR11 — initial status
            CustomerId = customerId,
            AssignedAgentId = null,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
