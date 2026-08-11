namespace TicketSystem.Application.Features.Auth.Commands.Login;

/// <summary>
/// Response shape returned by BOTH login (Story 2.3) and refresh (Story 2.4).
/// User is always included — clients rely on it after refresh to update display info.
/// </summary>
public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);
