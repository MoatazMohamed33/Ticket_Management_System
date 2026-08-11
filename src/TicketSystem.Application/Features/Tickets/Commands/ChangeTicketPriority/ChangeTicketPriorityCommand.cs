using MediatR;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.ChangeTicketPriority;

public sealed record ChangeTicketPriorityCommand(
    Guid TicketId,
    TicketPriority Priority,
    string RowVersion) : IRequest<TicketDto>;
