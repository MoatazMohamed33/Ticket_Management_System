using MediatR;

namespace TicketSystem.Application.Features.Tickets.Commands.AssignTicket;

public sealed record AssignTicketCommand(
    Guid TicketId,
    Guid? AssignedAgentId,
    string RowVersion) : IRequest<TicketDto>;
