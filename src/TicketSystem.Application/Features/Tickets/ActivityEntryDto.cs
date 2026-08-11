using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets;

public sealed record ActivityEntryDto(
    Guid Id,
    UserSummaryDto Actor,
    ActivityEventType Event,
    string Summary,
    DateTimeOffset At);
