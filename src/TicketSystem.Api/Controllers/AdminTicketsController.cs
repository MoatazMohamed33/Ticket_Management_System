using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Application.Features.Tickets.Commands.AssignTicket;
using TicketSystem.Application.Features.Tickets.Commands.ChangeTicketPriority;
using TicketSystem.Application.Features.Tickets.Queries.ListAllTickets;

namespace TicketSystem.Api.Controllers;

/// <summary>
/// Admin-only ticket operations. Extended by Stories 6.2 (priority) and 6.3 (assign).
/// Parallel to Epic 3's AdminUsersController — grouping admin ops under /api/admin/*
/// simplifies bulk-securing at ops layer (e.g., IP allowlist on the whole prefix).
/// </summary>
[ApiController]
[Route("api/admin/tickets")]
public sealed class AdminTicketsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdminTicketsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Org-wide ticket list with filters, search, and sort (FR14, FR23).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PagedResult<TicketListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] ListAllTicketsQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    /// <summary>Change a ticket's priority (FR17). Optimistic-concurrency enforced.</summary>
    [HttpPatch("{id:guid}/priority")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin-write")]   // Story 7.3
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangePriority(
        Guid id, ChangeTicketPriorityRequest body, CancellationToken ct) =>
        Ok(await _mediator.Send(
            new ChangeTicketPriorityCommand(id, body.Priority, body.RowVersion), ct));

    /// <summary>Assign, reassign, or unassign a ticket (FR18). Target must be an active
    /// Support Agent, or null to unassign.</summary>
    [HttpPatch("{id:guid}/assign")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin-write")]   // Story 7.3
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Assign(
        Guid id, AssignTicketRequest body, CancellationToken ct) =>
        Ok(await _mediator.Send(
            new AssignTicketCommand(id, body.AssignedAgentId, body.RowVersion), ct));
}
