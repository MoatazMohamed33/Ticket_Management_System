using MediatR;

namespace TicketSystem.Application.Features.Tickets.Commands.LogTime;

public sealed record LogTimeCommand(
    Guid TicketId,
    DateOnly WorkedOn,
    int DurationMinutes,
    string Description) : IRequest<TimeEntryDto>;
