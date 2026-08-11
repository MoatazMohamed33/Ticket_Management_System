using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Features.Tickets;

namespace TicketSystem.Infrastructure.Persistence;

/// <summary>
/// Ticket-detail read that projects the aggregate + child collections in four sequential
/// queries. Parallel Task.WhenAll on the same DbContext throws — EF Core is not
/// thread-safe on a single context instance.
/// </summary>
public sealed class TicketDetailQueries : ITicketDetailQueries
{
    private readonly AppDbContext _db;
    public TicketDetailQueries(AppDbContext db) => _db = db;

    public async Task<TicketDetailDto?> GetAsync(Guid ticketId, CancellationToken ct = default)
    {
        // Root ticket + user summaries in one projection.
        var head = await _db.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new
            {
                t.Id, t.Title, t.Description, t.Status, t.Priority,
                t.CreatedAt, t.UpdatedAt, t.RowVersion,
                Customer = _db.Users
                    .Where(u => u.Id == t.CustomerId)
                    .Select(u => new UserSummaryDto(u.Id, u.DisplayName))
                    .FirstOrDefault(),
                AssignedAgent = t.AssignedAgentId == null
                    ? null
                    : _db.Users
                        .Where(u => u.Id == t.AssignedAgentId)
                        .Select(u => new UserSummaryDto(u.Id, u.DisplayName))
                        .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (head is null || head.Customer is null) return null;

        var comments = await _db.Comments.AsNoTracking()
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(
                c.Id,
                _db.Users.Where(u => u.Id == c.AuthorUserId)
                    .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).First(),
                c.Body,
                c.CreatedAt))
            .ToListAsync(ct);

        var activity = await _db.ActivityEntries.AsNoTracking()
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.At)
            .Select(a => new ActivityEntryDto(
                a.Id,
                _db.Users.Where(u => u.Id == a.ActorUserId)
                    .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).First(),
                a.Event,
                a.Summary,
                a.At))
            .ToListAsync(ct);

        var timeEntries = await _db.TimeEntries.AsNoTracking()
            .Where(te => te.TicketId == ticketId)
            .OrderBy(te => te.WorkedOn)
            .Select(te => new TimeEntryDto(
                te.Id,
                _db.Users.Where(u => u.Id == te.AuthorUserId)
                    .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).First(),
                te.WorkedOn,
                te.DurationMinutes,
                te.Description,
                te.CreatedAt))
            .ToListAsync(ct);

        var totalMinutes = timeEntries.Sum(t => t.DurationMinutes);

        return new TicketDetailDto(
            head.Id, head.Title, head.Description, head.Status, head.Priority,
            head.Customer, head.AssignedAgent,
            head.CreatedAt, head.UpdatedAt, Convert.ToBase64String(head.RowVersion),
            comments, activity, timeEntries, totalMinutes);
    }
}
