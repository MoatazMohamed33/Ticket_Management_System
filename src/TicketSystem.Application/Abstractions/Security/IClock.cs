namespace TicketSystem.Application.Abstractions.Security;

/// <summary>Injectable clock abstraction — enables deterministic time in tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
