namespace TicketSystem.Application.Features.Tickets.Commands.AssignTicket;

public sealed record AssignTicketRequest(Guid? AssignedAgentId, string RowVersion);
