using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.ChangeTicketPriority;

public sealed record ChangeTicketPriorityRequest(TicketPriority Priority, string RowVersion);
