namespace TicketSystem.Application.Features.Dashboard;

/// <summary>
/// Aggregate dashboard summary for Story 6.5's admin page. Four operational metrics
/// (FR31–FR34) computed with a configurable time window on averageResolutionMinutes.
/// </summary>
public sealed record DashboardSummaryDto(
    IReadOnlyDictionary<string, int> TicketCountsByStatus,   // "Open" → 12, etc. Always all 4 keys.
    int OpenCriticalCount,
    int? AverageResolutionMinutes,
    IReadOnlyList<AgentWorkloadDto> AgentWorkload);

public sealed record AgentWorkloadDto(
    Guid AgentId,
    string AgentDisplayName,
    int OpenCount,
    int InProgressCount);
