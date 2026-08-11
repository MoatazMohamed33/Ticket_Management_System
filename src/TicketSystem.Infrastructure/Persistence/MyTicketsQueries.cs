using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Infrastructure.Persistence;

public sealed class MyTicketsQueries : IMyTicketsQueries
{
    private readonly AppDbContext _db;
    public MyTicketsQueries(AppDbContext db) => _db = db;

    public async Task<PagedResult<TicketListItemDto>> ListForCustomerAsync(
        Guid customerId,
        int page,
        int pageSize,
        TicketStatus[]? statuses,
        TicketPriority[]? priorities,
        string? search,
        string sortBy,
        string sortDirection,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // FR21 hard filter applied FIRST — every downstream clause narrows within this scope.
        IQueryable<Ticket> q = _db.Tickets.AsNoTracking()
            .Where(t => t.CustomerId == customerId);

        if (statuses is { Length: > 0 })
        {
            q = q.Where(t => statuses.Contains(t.Status));
        }

        if (priorities is { Length: > 0 })
        {
            q = q.Where(t => priorities.Contains(t.Priority));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var raw = search.Trim();
            // Escape LIKE wildcards on both branches. Bracket first — order matters.
            var escaped = raw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
            var pattern = $"%{escaped}%";
            // Case-insensitive collation → sargable, no per-row UPPER(). Same treatment
            // as Story 3.2 review fix H1.
            q = q.Where(t =>
                EF.Functions.Like(EF.Functions.Collate(t.Title, "SQL_Latin1_General_CP1_CI_AS"), pattern) ||
                EF.Functions.Like(EF.Functions.Collate(t.Description, "SQL_Latin1_General_CP1_CI_AS"), pattern));
        }

        // Default is descending per Story 4.2 AC-7. Only an explicit "asc" flips to ascending;
        // unknown values (typos, empty) fall through to the default direction.
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        q = (sortBy?.ToLowerInvariant()) switch
        {
            "createdat" => descending ? q.OrderByDescending(t => t.CreatedAt) : q.OrderBy(t => t.CreatedAt),
            "priority"  => descending ? q.OrderByDescending(t => t.Priority)  : q.OrderBy(t => t.Priority),
            _           => descending ? q.OrderByDescending(t => t.UpdatedAt) : q.OrderBy(t => t.UpdatedAt),
        };

        var totalCount = await q.LongCountAsync(ct);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.CreatedAt,
                t.UpdatedAt,
                // Correlated sub-select for agent display name — single query, no N+1.
                _db.Users.Where(u => u.Id == t.AssignedAgentId).Select(u => u.DisplayName).FirstOrDefault(),
                // Customer-scoped list: caller IS the customer, so CustomerDisplayName is intentionally null.
                null,
                t.AssignedAgentId,
                Convert.ToBase64String(t.RowVersion)))
            .ToListAsync(ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<TicketListItemDto>(items, page, pageSize, totalCount, totalPages);
    }
}
