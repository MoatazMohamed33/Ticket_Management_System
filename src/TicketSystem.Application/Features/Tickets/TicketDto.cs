using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets;

/// <summary>
/// Wire DTO for a ticket. RowVersion is base64-encoded so clients can echo it back on
/// mutating endpoints without decoding — server converts to bytes when applying the
/// optimistic-concurrency check.
/// </summary>
public sealed record TicketDto(
    Guid Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    Guid CustomerId,
    Guid? AssignedAgentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string RowVersion);
