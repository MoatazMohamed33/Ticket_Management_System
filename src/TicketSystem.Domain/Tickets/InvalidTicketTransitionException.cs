namespace TicketSystem.Domain.Tickets;

/// <summary>
/// Thrown when a state transition violates the ticket state machine. Middleware maps
/// this to 400 ProblemDetails with the offending From/To in extensions.
/// </summary>
public sealed class InvalidTicketTransitionException : Exception
{
    public TicketStatus From { get; }
    public TicketStatus To { get; }

    public InvalidTicketTransitionException(TicketStatus from, TicketStatus to)
        : base($"Cannot transition ticket from {from} to {to}.")
    {
        From = from;
        To = to;
    }
}
