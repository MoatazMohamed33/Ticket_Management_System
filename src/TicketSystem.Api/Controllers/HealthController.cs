using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Application.Features.Health.Queries.GetDeepHealth;
using TicketSystem.Application.Features.Health.Queries.GetHealth;

namespace TicketSystem.Api.Controllers;

[ApiController]
[Route("api/health")]
// NO class-level [AllowAnonymous] — per Story 2.6 finding, class-level adds IAllowAnonymous
// metadata to every action which can silently short-circuit any future method-level
// [Authorize] additions. Per-method attribution is enforced by an architecture guardrail.
public sealed class HealthController : ControllerBase
{
    private readonly IMediator _mediator;

    public HealthController(IMediator mediator) => _mediator = mediator;

    /// <summary>Liveness check — does not touch the database.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HealthResponseDto), StatusCodes.Status200OK)]
    public IActionResult Get() =>
        Ok(new HealthResponseDto(
            "ok",
            typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            DateTimeOffset.UtcNow));

    /// <summary>Deep health — includes a database reachability check with latency.</summary>
    [HttpGet("deep")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HealthDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDeep(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetDeepHealthQuery(), ct));
}
