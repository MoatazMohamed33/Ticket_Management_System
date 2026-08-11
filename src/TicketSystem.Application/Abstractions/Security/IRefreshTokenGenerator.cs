namespace TicketSystem.Application.Abstractions.Security;

public interface IRefreshTokenGenerator
{
    /// <summary>Returns a fresh raw token (returned to client) and its persistable hash.</summary>
    (string RawToken, string Hash) Generate();
}
