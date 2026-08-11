using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using TicketSystem.DataIsolationTests.Infrastructure;

namespace TicketSystem.DataIsolationTests.Isolation;

[Collection("Isolation")]
public class TokenTamperingTests
{
    private readonly IsolationApiFactory _factory;

    public TokenTamperingTests(IsolationApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Jwt_signed_with_wrong_key_returns_401()
    {
        var client = _factory.CreateClient();

        // Forge a token that CLAIMS role=Admin but is signed with a key the server does
        // not trust. Server's signature validation must reject it before role-based
        // authorization runs.
        var wrongKey = Convert.FromBase64String(
            "d3Jvbmcta2V5LWZvci10YW1wZXJpbmctdGVzdC0zMi1ieXRlcyE=");
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(wrongKey), SecurityAlgorithms.HmacSha256);

        var forged = new JwtSecurityToken(
            issuer: "isolation-tests",
            audience: "isolation-tests",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, _factory.Fixture.CustomerA.Id.ToString()),
                new Claim(ClaimTypes.Role, "Admin"),   // maliciously elevated
            },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(forged);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var res = await client.GetAsync("/api/admin/users");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "invalid signature must fail authentication before authorization runs");
    }

    [Fact]
    public async Task Expired_jwt_returns_401()
    {
        var client = _factory.CreateClient();

        var validKey = Convert.FromBase64String(IsolationApiFactory.SigningKeyBase64);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(validKey), SecurityAlgorithms.HmacSha256);

        var expired = new JwtSecurityToken(
            issuer: "isolation-tests",
            audience: "isolation-tests",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, _factory.Fixture.CustomerA.Id.ToString()),
                new Claim(ClaimTypes.Role, "Customer"),
            },
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),   // expired an hour ago
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(expired);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var res = await client.GetAsync("/api/tickets");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
