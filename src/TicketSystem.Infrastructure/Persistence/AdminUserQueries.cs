using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.AdminUsers;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence;

public sealed class AdminUserQueries : IAdminUserQueries
{
    private readonly AppDbContext _db;
    public AdminUserQueries(AppDbContext db) => _db = db;

    public async Task<PagedResult<AdminUserDto>> ListAsync(
        int page,
        int pageSize,
        UserRole? role,
        string? search,
        string sortBy,
        string sortDirection,
        CancellationToken ct = default)
    {
        // Clamp — tolerant of client typos rather than 400
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<User> q = _db.Users.AsNoTracking();

        if (role.HasValue)
        {
            q = q.Where(u => u.Role == role.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Email is stored pre-normalized to UPPER — case-fold the search term to match.
            var upper = search.Trim().ToUpperInvariant();
            var raw = search.Trim();

            // Escape LIKE wildcards on both branches so `_` and `%` in the search term don't
            // widen the match. Bracket must be escaped first — order matters.
            string Escape(string s) => s
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("_", "[_]");

            var upperPattern = $"%{Escape(upper)}%";
            var rawPattern = $"%{Escape(raw)}%";

            // DisplayName branch uses a case-insensitive collation so SQL Server can still
            // use any index on DisplayName instead of a per-row UPPER() (non-sargable).
            q = q.Where(u =>
                EF.Functions.Like(u.EmailNormalized, upperPattern) ||
                EF.Functions.Like(EF.Functions.Collate(u.DisplayName, "SQL_Latin1_General_CP1_CI_AS"), rawPattern));
        }

        // Default is descending per Story 3.2. Only an explicit "asc" flips; unknown values fall through.
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        q = (sortBy?.ToLowerInvariant()) switch
        {
            "displayname" => descending ? q.OrderByDescending(u => u.DisplayName) : q.OrderBy(u => u.DisplayName),
            "email"       => descending ? q.OrderByDescending(u => u.Email)       : q.OrderBy(u => u.Email),
            "role"        => descending ? q.OrderByDescending(u => u.Role)        : q.OrderBy(u => u.Role),
            _             => descending ? q.OrderByDescending(u => u.CreatedAt)   : q.OrderBy(u => u.CreatedAt),
        };

        var totalCount = await q.LongCountAsync(ct);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync(ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<AdminUserDto>(items, page, pageSize, totalCount, totalPages);
    }
}
