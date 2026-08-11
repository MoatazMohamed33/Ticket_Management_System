using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Domain.Users;

namespace TicketSystem.Infrastructure.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _opts;
    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenGenerator(IOptions<JwtOptions> opts, IClock clock)
    {
        _opts = opts.Value;
        _clock = clock;

        // Key validation ran at startup via PostConfigure — safe to decode here.
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_opts.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user)
    {
        var expires = _clock.UtcNow.AddMinutes(_opts.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("displayName", user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var jwt = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: _clock.UtcNow.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return (token, expires);
    }
}
