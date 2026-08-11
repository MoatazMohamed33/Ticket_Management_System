namespace TicketSystem.Application.Common.Exceptions;

/// <summary>
/// Thrown by auth handlers on any credential-related failure (unknown email, wrong password,
/// disabled account, invalid/expired/revoked refresh token). Middleware maps to 401 with a
/// deliberately generic message — never disclose which specific check failed.
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message = "Invalid credentials.") : base(message) { }
}
