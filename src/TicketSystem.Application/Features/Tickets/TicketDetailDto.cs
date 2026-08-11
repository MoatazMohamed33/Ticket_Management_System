using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets;

public sealed record TicketDetailDto(
    Guid Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    UserSummaryDto Customer,
    UserSummaryDto? AssignedAgent,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string RowVersion,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<ActivityEntryDto> Activity,
    IReadOnlyList<TimeEntryDto> TimeEntries,
    int TotalLoggedMinutes);
