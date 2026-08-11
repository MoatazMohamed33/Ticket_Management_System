namespace TicketSystem.Domain.Tickets;

public sealed class Comment
{
    private Comment() { }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public static Comment Create(Guid ticketId, Guid authorUserId, string body, DateTimeOffset now)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("TicketId is required.", nameof(ticketId));
        if (authorUserId == Guid.Empty)
            throw new ArgumentException("AuthorUserId is required.", nameof(authorUserId));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        return new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Body = body.Trim(),
            CreatedAt = now,
        };
    }
}
