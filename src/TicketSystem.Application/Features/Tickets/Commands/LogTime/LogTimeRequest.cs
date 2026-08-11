namespace TicketSystem.Application.Features.Tickets.Commands.LogTime;

public sealed record LogTimeRequest(DateOnly WorkedOn, int DurationMinutes, string Description);
