using MediatR;

namespace TicketSystem.Application.Features.Dashboard.Queries.GetDashboardSummary;

/// <summary>Window syntax: "7d", "30d", "90d", "all". Malformed → 30d fallback.</summary>
public sealed record GetDashboardSummaryQuery(string? Window = "30d") : IRequest<DashboardSummaryDto>;
