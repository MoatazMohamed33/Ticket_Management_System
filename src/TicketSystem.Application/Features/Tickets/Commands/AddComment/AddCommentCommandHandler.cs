using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.AddComment;

public sealed class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, CommentDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUserSummaryQueries _userSummaries;
    private readonly ITicketBroadcaster _broadcaster;
    private readonly ILogger<AddCommentCommandHandler> _logger;

    public AddCommentCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        IUserSummaryQueries userSummaries,
        ITicketBroadcaster broadcaster,
        ILogger<AddCommentCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _clock = clock;
        _userSummaries = userSummaries;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<CommentDto> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        // Load tracked — we'll bump the ticket's UpdatedAt.
        var ticket = await _uow.Repository<Ticket>()
            .FirstOrDefaultAsync(t => t.Id == cmd.TicketId, ct);
        if (ticket is null) throw new NotFoundException("Ticket not found.");

        // FR21 — same 404 message for ownership fail as for not-found; no existence leak.
        if (!TicketAccess.CanView(ticket.CustomerId, ticket.AssignedAgentId, _currentUser))
            throw new NotFoundException("Ticket not found.");

        if (_currentUser.UserId is not Guid authorId)
            throw new UnauthorizedAccessException();

        var now = _clock.UtcNow;
        var comment = Comment.Create(ticket.Id, authorId, cmd.Body, now);
        await _uow.Repository<Comment>().AddAsync(comment, ct);

        // Bump ticket UpdatedAt so the customer's list (Story 4.2 sort by updatedAt) surfaces it.
        ticket.TouchUpdatedAt(now);

        await _uow.SaveChangesAsync(ct);

        var author = await _userSummaries.GetByIdAsync(authorId, ct)
            ?? throw new InvalidOperationException("Comment author summary not found — user was authenticated but user row missing.");

        // Story 7.1 — broadcast to ticket-{id} group so all detail-page viewers refetch.
        await _broadcaster.CommentAddedAsync(ticket.Id, comment.Id, ct);

        _logger.LogInformation(
            "User {AuthorId} added comment {CommentId} to ticket {TicketId}.",
            authorId, comment.Id, ticket.Id);

        return new CommentDto(comment.Id, author, comment.Body, comment.CreatedAt);
    }
}
