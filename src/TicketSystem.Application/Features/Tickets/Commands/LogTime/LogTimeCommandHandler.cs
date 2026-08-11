using MediatR;
using Microsoft.Extensions.Logging;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Realtime;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Application.Features.Tickets.Commands.LogTime;

public sealed class LogTimeCommandHandler : IRequestHandler<LogTimeCommand, TimeEntryDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUserSummaryQueries _userSummaries;
    private readonly ITicketBroadcaster _broadcaster;
    private readonly ILogger<LogTimeCommandHandler> _logger;

    public LogTimeCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        IUserSummaryQueries userSummaries,
        ITicketBroadcaster broadcaster,
        ILogger<LogTimeCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _clock = clock;
        _userSummaries = userSummaries;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<TimeEntryDto> Handle(LogTimeCommand cmd, CancellationToken ct)
    {
        var ticket = await _uow.Repository<Ticket>()
            .FirstOrDefaultAsync(t => t.Id == cmd.TicketId, ct);
        if (ticket is null) throw new NotFoundException("Ticket not found.");

        // Same rule as status mutation — Agent must be assigned; Admin always allowed.
        if (!TicketAccess.CanMutateStatus(ticket.CustomerId, ticket.AssignedAgentId, _currentUser))
            throw new NotFoundException("Ticket not found.");

        if (_currentUser.UserId is not Guid authorId)
            throw new UnauthorizedAccessException();

        var now = _clock.UtcNow;
        var entry = TimeEntry.Create(
            ticket.Id, authorId, cmd.WorkedOn, cmd.DurationMinutes, cmd.Description, now);

        await _uow.Repository<TimeEntry>().AddAsync(entry, ct);
        ticket.TouchUpdatedAt(now);   // parent gets a recent-activity bump
        await _uow.SaveChangesAsync(ct);

        var author = await _userSummaries.GetByIdAsync(authorId, ct)
            ?? throw new InvalidOperationException(
                "TimeEntry author summary not found — user was authenticated but user row missing.");

        // Story 7.1 — broadcast to ticket-{id} viewers.
        await _broadcaster.TimeEntryLoggedAsync(ticket.Id, entry.Id, ct);

        _logger.LogInformation(
            "User {AuthorId} logged {Minutes} min on ticket {TicketId} for {WorkedOn}.",
            authorId, entry.DurationMinutes, ticket.Id, entry.WorkedOn);

        return new TimeEntryDto(
            entry.Id, author, entry.WorkedOn, entry.DurationMinutes, entry.Description, entry.CreatedAt);
    }
}
