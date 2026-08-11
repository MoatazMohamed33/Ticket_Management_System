using MediatR;
using TicketSystem.Application.Features.Tickets;

namespace TicketSystem.Application.Features.Tickets.Queries.GetTicketDetail;

public sealed record GetTicketDetailQuery(Guid TicketId) : IRequest<TicketDetailDto>;
