using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Application.Features.Dashboard;
using TicketSystem.Application.Features.Dashboard.Queries.GetDashboardSummary;

namespace TicketSystem.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdminDashboardController(IMediator mediator) => _mediator = mediator;

    /// <summary>Aggregate dashboard summary — status counts, open Criticals, avg
    /// resolution over window, agent workload. Cached in-memory with per-write invalidation.</summary>
    [HttpGet("summary")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Summary(
        [FromQuery] string? window, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetDashboardSummaryQuery(window), ct));
}
