namespace TicketSystem.Domain.Tickets;

public sealed class TimeEntry
{
    private TimeEntry() { }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public DateOnly WorkedOn { get; private set; }
    public int DurationMinutes { get; private set; }
    public string Description { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public static TimeEntry Create(
        Guid ticketId,
        Guid authorUserId,
        DateOnly workedOn,
        int durationMinutes,
        string description,
        DateTimeOffset now)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("TicketId is required.", nameof(ticketId));
        if (authorUserId == Guid.Empty)
            throw new ArgumentException("AuthorUserId is required.", nameof(authorUserId));
        if (durationMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Must be > 0.");
        if (durationMinutes > 24 * 60)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Exceeds a single day.");
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        return new TimeEntry
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            WorkedOn = workedOn,
            DurationMinutes = durationMinutes,
            Description = description.Trim(),
            CreatedAt = now,
        };
    }
}
