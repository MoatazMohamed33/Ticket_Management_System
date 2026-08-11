namespace TicketSystem.Application.Features.Tickets;

public sealed record CommentDto(Guid Id, UserSummaryDto Author, string Body, DateTimeOffset CreatedAt);
