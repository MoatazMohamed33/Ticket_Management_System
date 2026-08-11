namespace TicketSystem.Application.Features.Tickets.Commands.AddComment;

/// <summary>Body DTO for POST /api/tickets/{id}/comments. Kept separate from the command
/// so the route parameter is the sole source of ticket identity.</summary>
public sealed record AddCommentRequest(string Body);
