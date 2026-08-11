namespace TicketSystem.Application.Features.Tickets;

public sealed record TimeEntryDto(
    Guid Id,
    UserSummaryDto Author,
    DateOnly WorkedOn,
    int DurationMinutes,
    string Description,
    DateTimeOffset CreatedAt);
