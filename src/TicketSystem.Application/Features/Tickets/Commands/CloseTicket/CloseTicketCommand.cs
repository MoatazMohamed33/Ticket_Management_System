using MediatR;

namespace TicketSystem.Application.Features.Tickets.Commands.CloseTicket;

public sealed record CloseTicketCommand(Guid TicketId, string RowVersion) : IRequest<TicketDto>;
