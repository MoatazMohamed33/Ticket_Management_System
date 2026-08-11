namespace TicketSystem.Application.Common.Exceptions;

/// <summary>
/// Thrown by Infrastructure when a SaveChanges hits an optimistic-concurrency mismatch.
/// Application handlers never see EF's DbUpdateConcurrencyException — the UoW wrapper
/// translates at the boundary so Application stays EF-free (arch test enforced).
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public IReadOnlyDictionary<string, object?> CurrentState { get; }

    public ConcurrencyConflictException(IReadOnlyDictionary<string, object?> currentState)
        : base("The resource was updated by another operation.")
    {
        CurrentState = currentState;
    }
}
