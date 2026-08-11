using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Features.Tickets;

namespace TicketSystem.Api.Realtime;

/// <summary>
/// Ticket real-time hub. Every connection requires an authenticated JWT (Authorize).
/// Group membership is server-authorized via the shared TicketAccess.CanView predicate — the
/// same predicate used by REST endpoints, so FR21 no-leak extends to live updates.
/// </summary>
[Authorize]
public sealed class TicketsHub : Hub
{
    private readonly ITicketDetailQueries _detailQueries;
    private readonly ILogger<TicketsHub> _logger;

    public TicketsHub(ITicketDetailQueries detailQueries, ILogger<TicketsHub> logger)
    {
        _detailQueries = detailQueries;
        _logger = logger;
    }

    public async Task SubscribeToTicket(Guid ticketId)
    {
        var caller = ReadCaller();
        var detail = await _detailQueries.GetAsync(ticketId, Context.ConnectionAborted);
        // FR21 no-leak: identical error text whether the ticket doesn't exist or the
        // caller isn't authorized to see it.
        if (detail is null || !TicketAccess.CanView(detail, caller))
            throw new HubException("Not found.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
    }

    public Task UnsubscribeFromTicket(Guid ticketId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");

    public async Task SubscribeToMyTickets()
    {
        var caller = ReadCaller();
        if (caller.UserId is not Guid userId)
            throw new HubException("Not authenticated.");

        var group = caller.Role switch
        {
            "Admin"        => "admins",
            "SupportAgent" => $"agent-{userId}",
            "Customer"     => $"customer-{userId}",
            _              => throw new HubException("Unknown role."),
        };
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    // Hub-scoped ICurrentUser adapter. IHttpContextAccessor.HttpContext is NOT reliably
    // available during hub method invocations (WebSocket transport uses Hub.Context.User),
    // so the Story 2.6 CurrentUser would return null here. This adapter reads from
    // Context.User (ClaimsPrincipal) and keeps TicketAccess.CanView reusable.
    private ICurrentUser ReadCaller() => new HubCurrentUser(Context.User);

    private sealed class HubCurrentUser : ICurrentUser
    {
        private readonly ClaimsPrincipal? _user;
        public HubCurrentUser(ClaimsPrincipal? user) => _user = user;

        public bool IsAuthenticated => _user?.Identity?.IsAuthenticated == true;

        public Guid? UserId
        {
            get
            {
                var sub = _user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                return Guid.TryParse(sub, out var id) ? id : null;
            }
        }

        public string? Role => _user?.FindFirst(ClaimTypes.Role)?.Value;
    }
}
