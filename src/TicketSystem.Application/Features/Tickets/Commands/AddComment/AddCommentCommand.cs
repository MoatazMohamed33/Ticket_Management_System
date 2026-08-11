using MediatR;

namespace TicketSystem.Application.Features.Tickets.Commands.AddComment;

public sealed record AddCommentCommand(Guid TicketId, string Body) : IRequest<CommentDto>;
