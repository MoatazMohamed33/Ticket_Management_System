using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets;

public static class TicketMappings
{
    public static TicketDto ToDto(this Ticket t) => new(
        t.Id,
        t.Title,
        t.Description,
        t.Status,
        t.Priority,
        t.CustomerId,
        t.AssignedAgentId,
        t.CreatedAt,
        t.UpdatedAt,
        Convert.ToBase64String(t.RowVersion));
}
