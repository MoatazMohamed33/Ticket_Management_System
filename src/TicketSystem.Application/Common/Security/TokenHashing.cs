using System.Security.Cryptography;
using System.Text;

namespace TicketSystem.Application.Common.Security;

/// <summary>
/// Shared token-hashing helper. Stories 2.3 (login/refresh row create), 2.4 (refresh lookup),
/// and 2.5 (logout lookup) all MUST use this helper — inlining a different SHA-256 encoding
/// anywhere silently mismatches string comparisons and breaks refresh/logout lookups.
/// </summary>
public static class TokenHashing
{
    /// <summary>Hex-encoded (uppercase) SHA-256 of the raw token. 64 characters.</summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
