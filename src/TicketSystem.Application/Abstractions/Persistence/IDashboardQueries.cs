using TicketSystem.Application.Features.Dashboard;

namespace TicketSystem.Application.Abstractions.Persistence;

public interface IDashboardQueries
{
    /// <summary>Compute the dashboard summary. <paramref name="window"/> null = all-time.</summary>
    Task<DashboardSummaryDto> GetSummaryAsync(TimeSpan? window, CancellationToken ct = default);
}
