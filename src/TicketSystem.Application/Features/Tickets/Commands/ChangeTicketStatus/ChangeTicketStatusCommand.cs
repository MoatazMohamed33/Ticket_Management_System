using MediatR;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.ChangeTicketStatus;

public sealed record ChangeTicketStatusCommand(
    Guid TicketId,
    TicketStatus Status,
    string RowVersion) : IRequest<TicketDto>;
