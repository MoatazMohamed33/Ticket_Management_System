using TicketSystem.Application.Features.Tickets;

namespace TicketSystem.Application.Abstractions.Persistence;

public interface ITicketDetailQueries
{
    /// <summary>Returns null if the ticket does not exist (handler translates to 404).</summary>
    Task<TicketDetailDto?> GetAsync(Guid ticketId, CancellationToken ct = default);
}
