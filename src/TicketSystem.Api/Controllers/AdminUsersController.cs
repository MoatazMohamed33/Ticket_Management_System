using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.AdminUsers;
using TicketSystem.Application.Features.AdminUsers.Commands.ChangeUserRole;
using TicketSystem.Application.Features.AdminUsers.Commands.CreateStaffUser;
using TicketSystem.Application.Features.AdminUsers.Commands.SetUserActive;
using TicketSystem.Application.Features.AdminUsers.Queries.ListUsers;

namespace TicketSystem.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
// NO class-level attributes — per-method Auth attribution enforced by AuthorizationTests.
public sealed class AdminUsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminUsersController(IMediator mediator) => _mediator = mediator;

    /// <summary>List users with pagination, role filter, search, and sort.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PagedResult<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] ListUsersQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    /// <summary>Create a Support Agent or Admin account (Customer role rejected — customers self-register).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin-write")]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create(CreateStaffUserCommand cmd, CancellationToken ct)
    {
        var user = await _mediator.Send(cmd, ct);
        return Created(string.Empty, user);
    }

    /// <summary>Deactivate a user. Revokes all their refresh tokens. Idempotent — re-runs still revoke tokens.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin-write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SetUserActiveCommand(id, false), ct);
        return NoContent();
    }

    /// <summary>Reactivate a deactivated user. Does NOT reissue tokens — user must log in again.</summary>
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin-write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SetUserActiveCommand(id, true), ct);
        return NoContent();
    }

    /// <summary>Change a user's role. Revokes their refresh tokens so a new JWT with the new role is minted on next login.</summary>
    [HttpPatch("{id:guid}/role")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("admin-write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeUserRoleRequest body, CancellationToken ct)
    {
        await _mediator.Send(new ChangeUserRoleCommand(id, body.Role), ct);
        return NoContent();
    }
}
