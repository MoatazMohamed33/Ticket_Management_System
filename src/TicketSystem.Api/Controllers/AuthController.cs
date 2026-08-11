using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketSystem.Application.Features.Auth.Commands.Login;
using TicketSystem.Application.Features.Auth.Commands.Logout;
using TicketSystem.Application.Features.Auth.Commands.RefreshToken;
using TicketSystem.Application.Features.Auth.Commands.RegisterCustomer;

namespace TicketSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
// NO class-level [AllowAnonymous] — it would add IAllowAnonymous metadata to every action,
// silently short-circuiting method-level [Authorize] on Logout (Story 2.5).
// Apply [AllowAnonymous] per method: Register, Login, Refresh.
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    /// <summary>Register a new Customer account (visitor self-service).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(RegisterCustomerCommand cmd, CancellationToken ct)
    {
        var user = await _mediator.Send(cmd, ct);
        return Created(string.Empty, user); // no meaningful Location; avoid a 404-ing header
    }

    /// <summary>Authenticate with email + password and receive an access token + refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginCommand cmd, CancellationToken ct) =>
        Ok(await _mediator.Send(cmd, ct));

    /// <summary>Exchange a valid refresh token for a new access token + rotated refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(RefreshTokenCommand cmd, CancellationToken ct) =>
        Ok(await _mediator.Send(cmd, ct));

    /// <summary>Revoke the presented refresh token. Idempotent — unknown/revoked tokens also return 204.</summary>
    [HttpPost("logout")]
    [Authorize]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(LogoutCommand cmd, CancellationToken ct)
    {
        await _mediator.Send(cmd, ct);
        return NoContent();
    }
}
