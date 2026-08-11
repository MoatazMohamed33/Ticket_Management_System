using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TicketSystem.Application.Abstractions.Security;

namespace TicketSystem.Api.Auth;

/// <summary>HttpContext-backed implementation of <see cref="ICurrentUser"/>.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public bool IsAuthenticated => _http.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var subValue = _http.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(subValue, out var id) ? id : null;
        }
    }

    public string? Role => _http.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
}
