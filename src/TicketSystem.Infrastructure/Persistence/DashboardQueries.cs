using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Features.Dashboard;
using TicketSystem.Domain.Tickets;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Persistence;

/// <summary>
/// Four sequential queries on one DbContext (EF Core is not thread-safe on a shared
/// context — no Task.WhenAll). All AsNoTracking. At MVP scale (≤ 50k tickets, ≤ 20
/// agents) the four round-trips comfortably fit within NFR-P2's 300 ms warm-cache budget.
/// </summary>
public sealed class DashboardQueries : IDashboardQueries
{
    private readonly AppDbContext _db;
    public DashboardQueries(AppDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> GetSummaryAsync(TimeSpan? window, CancellationToken ct = default)
    {
        // 1. Counts by status — GroupBy + fill-with-zeros for absent statuses.
        var raw = await _db.Tickets.AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byStatus = Enum.GetValues<TicketStatus>()
            .ToDictionary(s => s.ToString(), s => raw.FirstOrDefault(r => r.Key == s)?.Count ?? 0);

        // 2. Open Critical count.
        var openCritical = await _db.Tickets.AsNoTracking()
            .CountAsync(t => (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)
                         && t.Priority == TicketPriority.Critical, ct);

        // 3. Average resolution — fetch (Created, Closed) pairs within window and average
        // in-memory. Set is small (window bounded, MVP scale). Migrate to SQL AVG() via
        // FromSqlInterpolated if the closed-in-window set grows past ~10k rows.
        var cutoff = window is null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow - window.Value;
        var closedQuery = _db.Tickets.AsNoTracking()
            .Where(t => t.Status == TicketStatus.Closed && t.ClosedAt != null);
        if (cutoff is not null)
            closedQuery = closedQuery.Where(t => t.ClosedAt >= cutoff.Value);

        var pairs = await closedQuery
            .Select(t => new { t.CreatedAt, ClosedAt = t.ClosedAt!.Value })
            .ToListAsync(ct);
        int? avg = pairs.Count == 0
            ? (int?)null
            : (int)pairs.Average(p => (p.ClosedAt - p.CreatedAt).TotalMinutes);

        // 4. Agent workload — every active agent, including zero-load (dashboard shows full team).
        var workload = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.SupportAgent && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new AgentWorkloadDto(
                u.Id,
                u.DisplayName,
                _db.Tickets.Count(t => t.AssignedAgentId == u.Id && t.Status == TicketStatus.Open),
                _db.Tickets.Count(t => t.AssignedAgentId == u.Id && t.Status == TicketStatus.InProgress)))
            .ToListAsync(ct);

        return new DashboardSummaryDto(byStatus, openCritical, avg, workload);
    }
}
