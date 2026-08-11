using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Infrastructure.Persistence;

public sealed class MyAssignedTicketsQueries : IMyAssignedTicketsQueries
{
    private readonly AppDbContext _db;
    public MyAssignedTicketsQueries(AppDbContext db) => _db = db;

    public async Task<PagedResult<TicketListItemDto>> ListForAgentAsync(
        Guid agentId,
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

        // FR22 hard filter applied first.
        IQueryable<Ticket> q = _db.Tickets.AsNoTracking()
            .Where(t => t.AssignedAgentId == agentId);

        if (statuses is { Length: > 0 })
            q = q.Where(t => statuses.Contains(t.Status));

        if (priorities is { Length: > 0 })
            q = q.Where(t => priorities.Contains(t.Priority));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var raw = search.Trim();
            var escaped = raw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
            var pattern = $"%{escaped}%";
            q = q.Where(t =>
                EF.Functions.Like(EF.Functions.Collate(t.Title, "SQL_Latin1_General_CP1_CI_AS"), pattern) ||
                EF.Functions.Like(EF.Functions.Collate(t.Description, "SQL_Latin1_General_CP1_CI_AS"), pattern));
        }

        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        // "priority" (also default + unknown) does compound sort priority + updatedAt;
        // "updatedAt" and "createdAt" are single-column. Consistency between explicit and defaulted.
        q = (sortBy?.ToLowerInvariant()) switch
        {
            "updatedat" => descending ? q.OrderByDescending(t => t.UpdatedAt) : q.OrderBy(t => t.UpdatedAt),
            "createdat" => descending ? q.OrderByDescending(t => t.CreatedAt) : q.OrderBy(t => t.CreatedAt),
            _ /* priority or unknown */ => descending
                ? q.OrderByDescending(t => t.Priority).ThenByDescending(t => t.UpdatedAt)
                : q.OrderBy(t => t.Priority).ThenByDescending(t => t.UpdatedAt),
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
                _db.Users.Where(u => u.Id == t.AssignedAgentId).Select(u => u.DisplayName).FirstOrDefault(),
                // Agent-scoped list: surface the customer name so the Agent knows whose ticket this is.
                _db.Users.Where(u => u.Id == t.CustomerId).Select(u => u.DisplayName).FirstOrDefault(),
                t.AssignedAgentId,
                Convert.ToBase64String(t.RowVersion)))
            .ToListAsync(ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<TicketListItemDto>(items, page, pageSize, totalCount, totalPages);
    }
}
