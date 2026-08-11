using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.ChangeTicketStatus;

public sealed record ChangeTicketStatusRequest(TicketStatus Status, string RowVersion);
