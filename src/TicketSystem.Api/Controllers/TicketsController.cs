using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.Tickets;
using TicketSystem.Application.Features.Tickets.Commands.AddComment;
using TicketSystem.Application.Features.Tickets.Commands.ChangeTicketStatus;
using TicketSystem.Application.Features.Tickets.Commands.CloseTicket;
using TicketSystem.Application.Features.Tickets.Commands.CreateTicket;
using TicketSystem.Application.Features.Tickets.Commands.LogTime;
using TicketSystem.Application.Features.Tickets.Queries.GetTicketDetail;
using TicketSystem.Application.Features.Tickets.Queries.ListMyAssignedTickets;
using TicketSystem.Application.Features.Tickets.Queries.ListMyTickets;

namespace TicketSystem.Api.Controllers;

[ApiController]
[Route("api/tickets")]
// NO class-level attributes — per-method Auth attribution enforced by AuthorizationTests.
public sealed class TicketsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TicketsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Customer creates a ticket. Status defaults to Open; caller identity is authoritative.</summary>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    [EnableRateLimiting("ticket-create")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create(CreateTicketCommand cmd, CancellationToken ct)
    {
        var ticket = await _mediator.Send(cmd, ct);
        return Created(string.Empty, ticket);
    }

    /// <summary>List tickets owned by the calling Customer. Filter/search/sort/paginate.</summary>
    [HttpGet]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(PagedResult<TicketListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListMine([FromQuery] ListMyTicketsQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    /// <summary>List tickets assigned to the calling Agent (FR13). Default sort: priority DESC then updatedAt DESC.</summary>
    [HttpGet("assigned-to-me")]
    [Authorize(Roles = "SupportAgent")]
    [ProducesResponseType(typeof(PagedResult<TicketListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAssignedToMe(
        [FromQuery] ListMyAssignedTicketsQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    /// <summary>Get a ticket's full detail (metadata, comments, activity, time entries).
    /// Unauthorized ownership returns 404 (FR21 no-leak convention), not 403.</summary>
    [HttpGet("{id:guid}")]
    [Authorize]   // any authenticated role — TicketAccess.CanView is the gate; returns 404 on ownership fail.
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetTicketDetailQuery(id), ct));

    /// <summary>Post a comment on a ticket. Any authorized viewer may comment (FR25).
    /// Unauthorized ownership returns 404 (FR21).</summary>
    [HttpPost("{id:guid}/comments")]
    [Authorize]
    [EnableRateLimiting("comment-create")]   // Story 7.3 — split from ticket-create
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AddComment(Guid id, AddCommentRequest body, CancellationToken ct)
    {
        var comment = await _mediator.Send(new AddCommentCommand(id, body.Body), ct);
        return Created(string.Empty, comment);
    }

    /// <summary>Customer closes their own Resolved ticket (FR19). Requires rowVersion for
    /// optimistic-concurrency check (FR20).</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "Customer")]
    [EnableRateLimiting("comment-create")]   // Story 7.3 — close is a low-volume customer write, shares comment bucket
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Close(Guid id, CloseTicketRequest body, CancellationToken ct) =>
        Ok(await _mediator.Send(new CloseTicketCommand(id, body.RowVersion), ct));

    /// <summary>Agent (assigned) or Admin changes a ticket's status (FR16). Rejects
    /// illegal transitions with 400; stale rowVersion with 409.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [EnableRateLimiting("admin-write")]   // Story 7.3 — staff-write bucket (30/min/user)
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangeStatus(
        Guid id, ChangeTicketStatusRequest body, CancellationToken ct) =>
        Ok(await _mediator.Send(
            new ChangeTicketStatusCommand(id, body.Status, body.RowVersion), ct));

    /// <summary>Agent (assigned) or Admin logs a time entry against a ticket (FR28).
    /// Positive integer minutes ≤ 1440; date must be today or past.</summary>
    [HttpPost("{id:guid}/time-entries")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [EnableRateLimiting("time-entry-create")]   // Story 7.3 — split from ticket-create
    [ProducesResponseType(typeof(TimeEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LogTime(
        Guid id, LogTimeRequest body, CancellationToken ct)
    {
        var entry = await _mediator.Send(new LogTimeCommand(
            id, body.WorkedOn, body.DurationMinutes, body.Description), ct);
        return Created(string.Empty, entry);
    }
}
