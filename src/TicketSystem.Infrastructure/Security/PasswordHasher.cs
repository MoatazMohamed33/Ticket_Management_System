using Microsoft.AspNetCore.Identity;
using TicketSystem.Application.Abstractions.Security;

namespace TicketSystem.Infrastructure.Security;

/// <summary>
/// Adapter over ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>.
/// Uses PBKDF2-SHA256 with per-user salt (NFR-S1). No user entity is required
/// because Identity's hasher only needs a generic type parameter for API shape.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _inner = new();
    private static readonly object Sentinel = new();

    public string Hash(string password) => _inner.HashPassword(Sentinel, password);

    public bool Verify(string password, string hash)
    {
        // Identity's VerifyHashedPassword throws FormatException on malformed (non-base64) hashes.
        // Story 2.3's Login handler intentionally verifies against a dummy hash on lookup-miss
        // to defeat email-enumeration timing attacks; the dummy hash must never throw. Swallow
        // any exception and return false so callers get a uniform "invalid" answer.
        try
        {
            var result = _inner.VerifyHashedPassword(Sentinel, hash, password);
            return result is PasswordVerificationResult.Success
                        or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
