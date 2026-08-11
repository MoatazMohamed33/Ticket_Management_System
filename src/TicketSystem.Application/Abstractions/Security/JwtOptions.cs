namespace TicketSystem.Application.Abstractions.Security;

/// <summary>
/// JWT configuration. Bound from <c>Jwt</c> section in appsettings/env.
/// Validation lives in Infrastructure's PostConfigure — enforces base64 + 32-byte minimum.
/// </summary>
public sealed class JwtOptions
{
    public string SigningKey { get; init; } = "";
    public string Issuer { get; init; } = "ticket-system";
    public string Audience { get; init; } = "ticket-system-web";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}
