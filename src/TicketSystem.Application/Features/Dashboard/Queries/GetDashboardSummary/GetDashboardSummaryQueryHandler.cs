using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using TicketSystem.Application.Abstractions.Caching;
using TicketSystem.Application.Abstractions.Persistence;

namespace TicketSystem.Application.Features.Dashboard.Queries.GetDashboardSummary;

public sealed class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IDashboardQueries _queries;
    private readonly IMemoryCache _cache;
    private readonly IDashboardCacheInvalidator _cacheInvalidator;

    public GetDashboardSummaryQueryHandler(
        IDashboardQueries queries,
        IMemoryCache cache,
        IDashboardCacheInvalidator cacheInvalidator)
    {
        _queries = queries;
        _cache = cache;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery q, CancellationToken ct)
    {
        var window = ParseWindow(q.Window);
        var windowKey = window is null ? "all" : ((int)window.Value.TotalDays).ToString();
        var cacheKey = $"dashboard:v{_cacheInvalidator.CurrentVersion}:summary:{windowKey}";

        if (_cache.TryGetValue<DashboardSummaryDto>(cacheKey, out var hit) && hit is not null)
        {
            return hit;
        }

        var fresh = await _queries.GetSummaryAsync(window, ct);
        _cache.Set(cacheKey, fresh, TimeSpan.FromSeconds(30));   // Story 7.4 — PRD spec 30s; safety net; primary freshness via IDashboardCacheInvalidator
        return fresh;
    }

    private static TimeSpan? ParseWindow(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return TimeSpan.FromDays(30);
        if (string.Equals(raw, "all", StringComparison.OrdinalIgnoreCase)) return null;
        var m = Regex.Match(raw, @"^(\d+)d$", RegexOptions.IgnoreCase);   // "30D" ok too
        if (m.Success && int.TryParse(m.Groups[1].Value, out var days) && days > 0 && days <= 365)
            return TimeSpan.FromDays(days);
        return TimeSpan.FromDays(30);   // silent fallback for malformed
    }
}
