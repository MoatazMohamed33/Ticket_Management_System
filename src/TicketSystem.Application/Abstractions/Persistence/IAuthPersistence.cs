using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Abstractions.Persistence;

/// <summary>
/// Auth-specific persistence operations that need translation-aware EF Core features
/// (Include, ExecuteUpdate) which don't fit the generic repository shape.
/// Implemented in Infrastructure. Application handlers depend on this interface only.
/// </summary>
public interface IAuthPersistence
{
    /// <summary>Load a refresh token WITH its User eagerly included; no-tracking read.</summary>
    Task<RefreshToken?> FindRefreshTokenWithUserAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Atomic rotation. Sets RevokedAt = now + ReplacedByTokenId = newChildId on the parent
    /// IF the parent is still non-revoked at UPDATE time. Returns 1 if the caller won the
    /// race, 0 if another concurrent rotation already claimed the parent (loser should
    /// re-read and either use the winner's child via <see cref="FindReplacementForAsync"/>
    /// or fail).
    /// </summary>
    Task<int> TryRotateRefreshTokenAsync(
        string parentTokenHash,
        Guid newChildId,
        DateTimeOffset now,
        CancellationToken ct = default);

    /// <summary>Bulk-revoke every non-revoked token in the family. Returns count updated.</summary>
    Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Bulk-revoke every non-revoked refresh token belonging to a user. Called by Story 3.3
    /// on deactivation and Story 3.4 on role change — forces the user to re-authenticate.
    /// </summary>
    Task<int> RevokeAllUserTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Return the child token that replaced the given parent, if any. Used to detect the
    /// grace-window case where a legitimate network retry replays a just-rotated parent —
    /// we can identify the winner's child without nuking the family.
    /// </summary>
    Task<RefreshToken?> FindReplacementForAsync(Guid replacedTokenId, CancellationToken ct = default);
}
