using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Infrastructure.Persistence;

public sealed class AllTicketsQueries : IAllTicketsQueries
{
    private readonly AppDbContext _db;
    public AllTicketsQueries(AppDbContext db) => _db = db;

    public async Task<PagedResult<TicketListItemDto>> ListAsync(
        int page,
        int pageSize,
        TicketStatus[]? statuses,
        TicketPriority[]? priorities,
        Guid? assignedAgentId,
        bool assignedAgentIsUnassigned,
        Guid? customerId,
        string? search,
        string sortBy,
        string sortDirection,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // No ownership filter — Admin sees the whole organization (FR23).
        IQueryable<Ticket> q = _db.Tickets.AsNoTracking();

        if (statuses is { Length: > 0 })
            q = q.Where(t => statuses.Contains(t.Status));

        if (priorities is { Length: > 0 })
            q = q.Where(t => priorities.Contains(t.Priority));

        if (assignedAgentIsUnassigned)
            q = q.Where(t => t.AssignedAgentId == null);
        else if (assignedAgentId.HasValue)
            q = q.Where(t => t.AssignedAgentId == assignedAgentId.Value);

        if (customerId.HasValue)
            q = q.Where(t => t.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var raw = search.Trim();
            var escaped = raw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
            var pattern = $"%{escaped}%";
            q = q.Where(t =>
                EF.Functions.Like(EF.Functions.Collate(t.Title, "SQL_Latin1_General_CP1_CI_AS"), pattern) ||
                EF.Functions.Like(EF.Functions.Collate(t.Description, "SQL_Latin1_General_CP1_CI_AS"), pattern));
        }

        // Admin default is neutral "recent activity" — updatedAt desc. NOT the priority-compound
        // sort from Story 5.1 (that's agent-workspace-specific).
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        q = (sortBy?.ToLowerInvariant()) switch
        {
            "createdat" => descending ? q.OrderByDescending(t => t.CreatedAt) : q.OrderBy(t => t.CreatedAt),
            "priority"  => descending ? q.OrderByDescending(t => t.Priority)  : q.OrderBy(t => t.Priority),
            _ /* updatedAt or unknown */ => descending
                ? q.OrderByDescending(t => t.UpdatedAt)
                : q.OrderBy(t => t.UpdatedAt),
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
                _db.Users.Where(u => u.Id == t.CustomerId).Select(u => u.DisplayName).FirstOrDefault(),
                t.AssignedAgentId,
                Convert.ToBase64String(t.RowVersion)))
            .ToListAsync(ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<TicketListItemDto>(items, page, pageSize, totalCount, totalPages);
    }
}
